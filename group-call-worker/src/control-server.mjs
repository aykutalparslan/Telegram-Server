// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

import { createServer } from 'node:http';
import { randomUUID, timingSafeEqual } from 'node:crypto';

function equalSecret(actual, expected) {
  const left = Buffer.from(actual ?? '', 'utf8');
  const right = Buffer.from(expected, 'utf8');
  return left.length === right.length && timingSafeEqual(left, right);
}

function sendJson(response, status, value) {
  const body = Buffer.from(JSON.stringify(value), 'utf8');
  response.writeHead(status, {
    'content-type': 'application/json; charset=utf-8',
    'content-length': body.length
  });
  response.end(body);
}

function sendBytes(response, status, value) {
  response.writeHead(status, {
    'content-type': 'application/octet-stream',
    'content-length': value.length,
    'cache-control': 'private, max-age=300, immutable'
  });
  response.end(value);
}

function sendRecordingFile(response, file) {
  response.writeHead(200, {
    'content-type': file.mimeType,
    'content-length': file.length,
    'content-disposition': `attachment; filename="${file.fileName}"`,
    'x-ferrite-recording-duration': String(file.duration),
    'x-ferrite-recording-width': String(file.width),
    'x-ferrite-recording-height': String(file.height),
    'cache-control': 'private, no-store'
  });
  const stream = file.stream();
  stream.once('error', () => response.destroy());
  response.once('close', () => stream.destroy());
  stream.pipe(response);
}

async function readJson(request, maxBodyBytes) {
  const chunks = [];
  let length = 0;
  for await (const chunk of request) {
    length += chunk.length;
    if (length > maxBodyBytes) {
      const error = new Error('request body exceeds configured bound');
      error.statusCode = 413;
      throw error;
    }
    chunks.push(chunk);
  }
  if (length === 0) {
    return {};
  }
  try {
    return JSON.parse(Buffer.concat(chunks).toString('utf8'));
  } catch {
    const error = new Error('request body is not valid JSON');
    error.statusCode = 400;
    throw error;
  }
}

function parseCallId(text) {
  if (!/^[1-9][0-9]*$/.test(text)) {
    throw new TypeError('call id must be a positive integer');
  }
  const value = Number(text);
  if (!Number.isSafeInteger(value)) {
    throw new TypeError('call id is outside the safe integer range');
  }
  return value;
}

function errorStatus(error) {
  if (Number.isInteger(error?.statusCode)) {
    return error.statusCode;
  }
  if (error instanceof TypeError) {
    return 400;
  }
  if (error?.code === 'NOT_READY') {
    return 425;
  }
  if (error?.code === 'SEGMENT_EXPIRED') {
    return 404;
  }
  if (error?.code === 'SCALE_UNSUPPORTED' || error?.code === 'TIME_INVALID') {
    return 400;
  }
  if (error?.code === 'CAPACITY') {
    return 503;
  }
  if (error?.code === 'NOT_FOUND') {
    return 404;
  }
  if (error?.code === 'GENERATION_CONFLICT') {
    return 409;
  }
  if (error?.code === 'LIMIT') {
    return 413;
  }
  if (error?.code === 'INVALID_OUTPUT' || error?.code === 'UNAVAILABLE') {
    return 503;
  }
  const message = error?.message ?? '';
  if (message.includes('already joined') || message.includes('DUPLICATE') ||
      message.includes('not joined') || message.includes('missing')) {
    return 409;
  }
  if (message.includes('limit reached') || message.includes('not started') ||
      message.includes('worker died')) {
    return 503;
  }
  return 500;
}

export function createGroupCallControlServer({
  plane,
  broadcast,
  recording,
  authSecret,
  protocolVersion = '1',
  workerVersion = '3.21.2',
  maxBodyBytes = 256 * 1024,
  maxEventClients = 32,
  instanceId = randomUUID()
}) {
  if (!plane || typeof plane.health !== 'function') {
    throw new TypeError('control server requires a media plane');
  }
  if (typeof authSecret !== 'string' || authSecret.length < 16) {
    throw new TypeError('control auth secret must contain at least 16 characters');
  }

  const eventClients = new Set();
  const publishEvent = (event) => {
    const line = `${JSON.stringify(event)}\n`;
    for (const response of [...eventClients]) {
      // Never let Node grow an unbounded per-client write queue. A slow/dead
      // Ferrite reader is disconnected and reconnects through its bounded loop.
      if (!response.write(line)) {
        eventClients.delete(response);
        response.end();
      }
    }
  };

  const removeWorkerDeath = plane.onWorkerDeath?.((event) => {
    for (const correlation of event.correlations ?? []) {
      publishEvent({
        callId: correlation.callId,
        participantId: correlation.participantId,
        reason: 'worker_died'
      });
    }
  });
  const removeDisconnect = plane.onDisconnect?.((event) =>
    publishEvent({
      callId: event.callId,
      participantId: event.participantId,
      reason: event.reason
    }));
  // A mapping change carries no payload: the viewer matrix can be large, and a
  // bounded NDJSON line is the wrong place for it. Ferrite re-reads
  // /rooms/{id}/viewer-media instead.
  const removeSourcesChanged = plane.onSourcesChanged?.((event) => {
    // A re-created producer invalidates the broadcast/recording tap consumers
    // built from the old one.
    broadcast?.refreshSfu?.(event.callId)?.catch?.(() => {});
    publishEvent({
      callId: event.callId,
      participantId: event.participantId,
      reason: event.reason ?? 'sources_changed'
    });
  });

  const server = createServer(async (request, response) => {
    const authorization = request.headers.authorization ?? '';
    const suppliedProtocol =
        request.headers['x-ferrite-groupcall-protocol'] ?? '';
    if (!equalSecret(authorization, `Bearer ${authSecret}`) ||
        suppliedProtocol !== protocolVersion) {
      response.writeHead(401);
      response.end();
      return;
    }

    const url = new URL(request.url, 'http://control.invalid');
    try {
      if (request.method === 'GET' && url.pathname === '/health') {
        const health = await plane.health();
        const broadcastHealth = broadcast ? await broadcast.health() : null;
        const recordingHealth = recording ? await recording.health() : null;
        sendJson(response, 200, {
          healthy: Boolean(health.healthy),
          rooms: Number(health.rooms ?? 0),
          broadcast: broadcastHealth,
          recording: recordingHealth,
          instanceId,
          protocolVersion,
          workerVersion
        });
        return;
      }

      const recordingMatch = url.pathname.match(
          /^\/recordings\/([0-9]+)(?:\/(stop|ack))?$/);
      if (recordingMatch && recording) {
        const callId = parseCallId(recordingMatch[1]);
        const operation = recordingMatch[2] ?? '';
        if (!operation && request.method === 'PUT') {
          await recording.startRecording(callId,
              await readJson(request, maxBodyBytes));
          response.writeHead(204);
          response.end();
          return;
        }
        if (operation === 'stop' && request.method === 'POST') {
          const body = await readJson(request, maxBodyBytes);
          sendRecordingFile(response, await recording.finalizeRecording(
              callId, body.generation));
          return;
        }
        if (operation === 'ack' && request.method === 'POST') {
          const body = await readJson(request, maxBodyBytes);
          await recording.acknowledgeRecording(callId, body.generation);
          response.writeHead(204);
          response.end();
          return;
        }
        if (!operation && request.method === 'DELETE') {
          const generation = Number(url.searchParams.get('generation'));
          sendJson(response, 200, {
            cancelled: await recording.cancelRecording(callId, generation)
          });
          return;
        }
      }

      const broadcastMatch = url.pathname.match(
          /^\/broadcast\/([0-9]+)(?:\/(credentials|channels|segments))?$/);
      if (broadcastMatch && broadcast) {
        const callId = parseCallId(broadcastMatch[1]);
        const operation = broadcastMatch[2] ?? '';
        if (!operation && request.method === 'PUT') {
          const body = await readJson(request, maxBodyBytes);
          if (typeof body.rtmpStream !== 'boolean') {
            throw new TypeError('rtmpStream must be a boolean');
          }
          await broadcast.createStream(callId, {
            rtmpStream: body.rtmpStream
          });
          response.writeHead(204);
          response.end();
          return;
        }
        if (!operation && request.method === 'DELETE') {
          sendJson(response, 200, {
            ended: await broadcast.endStream(callId)
          });
          return;
        }
        if (operation === 'credentials' && request.method === 'POST') {
          const body = await readJson(request, maxBodyBytes);
          sendJson(response, 200, await broadcast.credentials(
              callId, Boolean(body.revoke)));
          return;
        }
        if (operation === 'channels' && request.method === 'GET') {
          sendJson(response, 200, { channels: broadcast.channels(callId) });
          return;
        }
        if (operation === 'segments' && request.method === 'GET') {
          const timestamp = Number(url.searchParams.get('timestamp'));
          const scale = Number(url.searchParams.get('scale'));
          const channel = Number(url.searchParams.get('channel'));
          const quality = Number(url.searchParams.get('quality') ?? 0);
          if (!Number.isSafeInteger(timestamp) || !Number.isInteger(scale) ||
              !Number.isInteger(channel) || !Number.isInteger(quality)) {
            throw new TypeError('segment query is invalid');
          }
          sendBytes(response, 200, await broadcast.readSegment(callId, {
            timestamp, scale, channel, quality
          }));
          return;
        }
      }
      if (request.method === 'GET' && url.pathname === '/events') {
        if (eventClients.size >= maxEventClients) {
          sendJson(response, 503, { error: 'event client limit reached' });
          return;
        }
        response.writeHead(200, {
          'content-type': 'application/x-ndjson; charset=utf-8',
          'cache-control': 'no-store',
          connection: 'keep-alive'
        });
        response.write('\n');
        eventClients.add(response);
        request.on('close', () => eventClients.delete(response));
        return;
      }

      // Re-read the per-viewer mapping after a sources_changed event. Join
      // responses already carry it; this is the only way to pick up a change
      // that no request caused.
      const viewerMediaMatch =
          url.pathname.match(/^\/rooms\/([0-9]+)\/viewer-media$/);
      if (viewerMediaMatch && request.method === 'GET') {
        const callId = parseCallId(viewerMediaMatch[1]);
        sendJson(response, 200, { viewerMedia: plane.getViewerMedia(callId) });
        return;
      }

      const roomMatch = url.pathname.match(/^\/rooms\/([0-9]+)$/);
      if (roomMatch) {
        const callId = parseCallId(roomMatch[1]);
        if (request.method === 'PUT') {
          await plane.createRoom(callId);
          response.writeHead(204);
          response.end();
          return;
        }
        if (request.method === 'DELETE') {
          sendJson(response, 200, { ended: await plane.endRoom(callId) });
          return;
        }
      }

      const participantMatch = url.pathname.match(
          /^\/rooms\/([0-9]+)\/participants\/([^/]+)(?:\/(presentation|mute|video-paused|liveness))?$/);
      if (participantMatch) {
        const callId = parseCallId(participantMatch[1]);
        const participantId = decodeURIComponent(participantMatch[2]);
        const operation = participantMatch[3] ?? '';
        if (participantId.length === 0 || participantId.length > 256) {
          throw new TypeError('participant id has invalid length');
        }

        if (!operation && request.method === 'POST') {
          const body = await readJson(request, maxBodyBytes);
          const joined = await plane.join(callId, participantId, body.payload);
          if (Array.isArray(body.payload?.['ssrc-groups'])) {
            // Attach the continuous broadcast video consumer before the join
            // answer lets the client send its first frame. That initial source
            // keyframe is the warm, shared input used by both broadcast and
            // mid-call recording.
            await broadcast?.refreshSfu?.(callId);
          }
          sendJson(response, 200, {
            connection: joined.connection,
            canonicalSource: joined.canonicalSource,
            viewerMedia: plane.getViewerMedia(callId)
          });
          return;
        }
        if (!operation && request.method === 'DELETE') {
          sendJson(response, 200, {
            left: await plane.leave(callId, participantId)
          });
          return;
        }
        if (operation === 'presentation' && request.method === 'POST') {
          const body = await readJson(request, maxBodyBytes);
          const joined = await plane.joinPresentation(
              callId, participantId, body.payload);
          if (Array.isArray(body.payload?.['ssrc-groups'])) {
            await broadcast?.refreshSfu?.(callId);
          }
          sendJson(response, 200, {
            connection: joined.connection,
            canonicalSource: joined.canonicalSource,
            viewerMedia: plane.getViewerMedia(callId)
          });
          return;
        }
        if (operation === 'presentation' && request.method === 'DELETE') {
          sendJson(response, 200, {
            left: await plane.leavePresentation(callId, participantId)
          });
          return;
        }
        if (operation === 'mute' && request.method === 'POST') {
          const body = await readJson(request, maxBodyBytes);
          await plane.setMuted(callId, participantId, Boolean(body.muted));
          response.writeHead(204);
          response.end();
          return;
        }
        if (operation === 'video-paused' && request.method === 'POST') {
          const body = await readJson(request, maxBodyBytes);
          await plane.setVideoPaused(callId, participantId,
              Boolean(body.paused));
          response.writeHead(204);
          response.end();
          return;
        }
        if (operation === 'liveness' && request.method === 'GET') {
          sendJson(response, 200, {
            alive: plane.isAlive(callId, participantId)
          });
          return;
        }
      }

      const tapMatch = url.pathname.match(
          /^\/rooms\/([0-9]+)\/taps\/([^/]+)$/);
      if (tapMatch) {
        const callId = parseCallId(tapMatch[1]);
        const subscriberId = decodeURIComponent(tapMatch[2]);
        if (request.method === 'POST') {
          const body = await readJson(request, maxBodyBytes);
          sendJson(response, 200, {
            taps: await plane.acquireRtpTap(
                callId, subscriberId, body.targets)
          });
          return;
        }
        if (request.method === 'DELETE') {
          sendJson(response, 200, {
            released: plane.releaseRtpTap(callId, subscriberId)
          });
          return;
        }
      }

      sendJson(response, 404, { error: 'not found' });
    } catch (error) {
      sendJson(response, errorStatus(error), {
        error: error?.message ?? 'media worker request failed'
      });
    }
  });

  const heartbeat = setInterval(() => {
    for (const response of [...eventClients]) {
      if (!response.write('\n')) {
        eventClients.delete(response);
        response.end();
      }
    }
  }, 15_000);
  heartbeat.unref();

  return {
    server,
    async listen({ host = '127.0.0.1', port = 9090 } = {}) {
      await new Promise((resolve, reject) => {
        server.once('error', reject);
        server.listen(port, host, () => {
          server.off('error', reject);
          resolve();
        });
      });
      return server.address();
    },
    async close() {
      clearInterval(heartbeat);
      removeWorkerDeath?.();
      removeDisconnect?.();
      removeSourcesChanged?.();
      for (const response of eventClients) {
        response.end();
      }
      eventClients.clear();
      if (server.listening) {
        await new Promise((resolve) => server.close(resolve));
      }
      await recording?.close?.();
      await broadcast?.close?.();
      await plane.close?.();
    },
    publishEvent
  };
}
