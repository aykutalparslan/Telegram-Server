// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

import { randomBytes } from 'node:crypto';
import { spawn, spawnSync } from 'node:child_process';
import { mkdir, readdir, readFile, rm, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import NodeMediaServer from 'node-media-server';
import nodeMediaLogger from 'node-media-server/src/node_core_logger.js';
import NodeRtmpServer from 'node-media-server/src/node_rtmp_server.js';
import nodeMediaContext from 'node-media-server/src/node_core_ctx.js';

import {
  encodeAudioMetadata,
  parseVideoSegment,
  wrapVideoSegment
} from './segment-format.mjs';
import { SegmentRing } from './segment-ring.mjs';
import { Vp9RtpIvfBridge } from './vp9-rtp-ivf.mjs';

const SEGMENT_DURATION_MS = 1000;

export class BroadcastError extends Error {
  constructor(code, message) {
    super(message);
    this.name = 'BroadcastError';
    this.code = code;
  }
}

function positiveInteger(value, name) {
  if (!Number.isInteger(value) || value < 1) {
    throw new TypeError(`${name} must be a positive integer`);
  }
  return value;
}

function safeCallId(callId) {
  if (!Number.isSafeInteger(callId) || callId <= 0) {
    throw new TypeError('callId must be a positive safe integer');
  }
  return callId;
}

function newStreamKey() {
  return randomBytes(24).toString('base64url');
}

export function buildRtmpFfmpegArguments({
  bindAddress,
  port,
  key,
  inputUrl,
  directory,
  audioMetadata,
  activeMask = 1,
  endpoints = 'unified'
}) {
  return [
    '-hide_banner', '-loglevel', 'warning', '-nostdin', '-y',
    '-i', inputUrl ?? `rtmp://${bindAddress}:${port}/live/${key}`,
    '-map', '0:a:0?', '-c:a', 'libopus', '-ar', '48000', '-ac', '1',
    '-b:a', '64k',
    '-metadata:s:a:0', `TG_META=${audioMetadata}`,
    '-metadata:s:a:0', `ACTIVE_MASK=${activeMask >>> 0}`,
    '-metadata:s:a:0', `ENDPOINTS=${endpoints}`,
    '-f', 'segment', '-segment_time', '1', '-reset_timestamps', '1',
    '-segment_format', 'ogg', '-segment_list_type', 'csv',
    '-segment_list', join(directory, 'audio.csv'),
    join(directory, 'audio-%09d.ogg'),
    '-map', '0:v:0?', '-c:v', 'copy',
    '-f', 'segment', '-segment_time', '1', '-reset_timestamps', '1',
    '-segment_format', 'mp4',
    '-segment_format_options', 'movflags=+faststart',
    '-segment_list_type', 'csv',
    '-segment_list', join(directory, 'video.csv'),
    join(directory, 'video-%09d.mp4')
  ];
}

function fmtp(parameters = {}) {
  return Object.entries(parameters)
      .filter(([, value]) => value !== undefined && value !== null &&
        String(value).length > 0)
      .map(([name, value]) => `${name}=${value}`)
      .join(';');
}

export function buildRtpTapSdp(descriptors) {
  if (!Array.isArray(descriptors) || descriptors.length === 0) {
    throw new TypeError('RTP tap descriptors must not be empty');
  }
  const lines = [
    'v=0',
    'o=ferrite 0 0 IN IP4 127.0.0.1',
    's=Ferrite group-call broadcast tap',
    't=0 0'
  ];
  for (const descriptor of descriptors) {
    const codecs = descriptor.rtpParameters?.codecs ?? [];
    const codec = codecs.find((value) =>
      value.mimeType?.toLowerCase().startsWith(`${descriptor.kind}/`) &&
      value.mimeType?.toLowerCase() !== 'video/rtx');
    if (!codec || !Number.isInteger(descriptor.targetPort)) {
      throw new TypeError('RTP tap descriptor has no usable codec or port');
    }
    const payloadType = codec.payloadType;
    const codecName = codec.mimeType.split('/')[1];
    const channels = descriptor.kind === 'audio' && codec.channels > 1
      ? `/${codec.channels}`
      : '';
    lines.push(
        `m=${descriptor.kind} ${descriptor.targetPort} RTP/AVP ${payloadType}`,
        'c=IN IP4 127.0.0.1',
        `a=rtpmap:${payloadType} ${codecName}/${codec.clockRate}${channels}`,
        'a=rtcp-mux',
        'a=recvonly');
    const parameters = fmtp(codec.parameters);
    if (parameters.length > 0) {
      lines.push(`a=fmtp:${payloadType} ${parameters}`);
    }
  }
  return `${lines.join('\r\n')}\r\n`;
}

export function buildRtpTapFfmpegArguments({
  sdpPath,
  directory,
  audioMetadata,
  activeMask,
  endpoints,
  vp9Pipe = false
}) {
  const input = ['-hide_banner', '-loglevel', 'warning', '-nostdin', '-y'];
  if (vp9Pipe) {
    input.push('-fflags', '+genpts+nobuffer', '-probesize', '32',
        '-analyzeduration', '0', '-f', 'ivf', '-i', 'pipe:3');
  }
  input.push('-protocol_whitelist', 'file,udp,rtp',
      '-fflags', '+genpts+nobuffer',
      // Internal taps are loopback-only and mediasoup already handles network
      // loss/reordering. Arrival-time PTS and a disabled RTP reorder queue keep
      // a sender sequence/timestamp discontinuity from freezing FFmpeg's
      // long-lived segment muxer after otherwise healthy packets resume.
      '-use_wallclock_as_timestamps', '1', '-reorder_queue_size', '0',
      '-f', 'sdp', '-i', sdpPath);
  return [
    ...input,
    '-map', vp9Pipe ? '1:a:0?' : '0:a:0?', '-c:a', 'libopus',
    '-ar', '48000', '-ac', '1',
    '-b:a', '64k',
    '-metadata:s:a:0', `TG_META=${audioMetadata}`,
    '-metadata:s:a:0', `ACTIVE_MASK=${activeMask >>> 0}`,
    '-metadata:s:a:0', `ENDPOINTS=${endpoints}`,
    '-f', 'segment', '-segment_time', '1', '-reset_timestamps', '1',
    '-segment_format', 'ogg', '-segment_list_type', 'csv',
    '-segment_list', join(directory, 'audio.csv'),
    join(directory, 'audio-%09d.ogg'),
    '-map', '0:v:0?', '-r', vp9Pipe ? '2' : '25', '-fps_mode', 'cfr',
    '-c:v', 'libx264',
    '-pix_fmt', 'yuv420p',
    '-preset', 'veryfast', '-tune', 'zerolatency', '-g', '300',
    '-keyint_min', '1', '-sc_threshold', '0',
    '-force_key_frames', 'expr:gte(t,n_forced*1)',
    '-f', 'segment', '-segment_time', '1', '-reset_timestamps', '1',
    '-segment_format', 'mp4',
    '-segment_format_options', 'movflags=+faststart',
    '-segment_list_type', 'csv',
    '-segment_list', join(directory, 'video.csv'),
    join(directory, 'video-%09d.mp4')
  ];
}

function parseCompletedList(value) {
  const result = [];
  for (const line of value.split(/\r?\n/)) {
    if (line.length === 0) {
      continue;
    }
    const match = /^(.*),(\d+(?:\.\d+)?),(\d+(?:\.\d+)?)$/.exec(line);
    if (!match) {
      continue;
    }
    const file = match[1].replace(/^"|"$/g, '');
    result.push({ file, start: Number(match[2]), end: Number(match[3]) });
  }
  return result;
}

export class SingleStreamRendition {
  constructor({ quality = 2 } = {}) {
    if (!Number.isInteger(quality) || quality < 0 || quality > 2) {
      throw new TypeError('rendition quality must be 0, 1, or 2');
    }
    this.quality = quality;
  }

  resolveQuality(requestedQuality) {
    if (!Number.isInteger(requestedQuality) || requestedQuality < 0 ||
        requestedQuality > 2) {
      throw new TypeError('requested quality must be 0, 1, or 2');
    }
    return this.quality;
  }
}

export class GroupCallBroadcastService {
  #root;
  #rtmpBindAddress;
  #rtmpAdvertisedAddress;
  #rtmpMinPort;
  #rtmpMaxPort;
  #retentionMs;
  #maxSegmentsPerCall;
  #maxBytesPerCall;
  #maxSegmentBytes;
  #ffmpegPath;
  #startIngest;
  #mediaPlane;
  #tapAddress;
  #tapMinPort;
  #tapMaxPort;
  #rtmpServer = null;
  #rtmpReady = null;
  #rtmpServerError = null;
  #rtmpListeners = null;
  #calls = new Map();
  #stopping = false;
  #rendition = new SingleStreamRendition();

  constructor({
    root,
    rtmpBindAddress = '127.0.0.1',
    rtmpAdvertisedAddress = '127.0.0.1',
    rtmpMinPort = 19350,
    rtmpMaxPort = 19449,
    retentionMs = 5 * 60 * 1000,
    maxSegmentsPerCall = 4096,
    maxBytesPerCall = 512 * 1024 * 1024,
    maxSegmentBytes = 1024 * 1024,
    ffmpegPath = 'ffmpeg',
    startIngest = true,
    mediaPlane = null,
    tapAddress = '127.0.0.1',
    tapMinPort = 50000,
    tapMaxPort = 50199
  }) {
    if (typeof root !== 'string' || root.length === 0 ||
        typeof rtmpBindAddress !== 'string' || rtmpBindAddress.length === 0 ||
        typeof rtmpAdvertisedAddress !== 'string' ||
        rtmpAdvertisedAddress.length === 0) {
      throw new TypeError('broadcast paths and addresses must not be empty');
    }
    positiveInteger(rtmpMinPort, 'rtmpMinPort');
    positiveInteger(rtmpMaxPort, 'rtmpMaxPort');
    positiveInteger(tapMinPort, 'tapMinPort');
    positiveInteger(tapMaxPort, 'tapMaxPort');
    if (rtmpMinPort > rtmpMaxPort || rtmpMaxPort > 65535) {
      throw new TypeError('invalid RTMP port range');
    }
    if (tapMinPort > tapMaxPort || tapMaxPort > 65535) {
      throw new TypeError('invalid RTP tap port range');
    }
    this.#root = root;
    this.#rtmpBindAddress = rtmpBindAddress;
    this.#rtmpAdvertisedAddress = rtmpAdvertisedAddress;
    this.#rtmpMinPort = rtmpMinPort;
    this.#rtmpMaxPort = rtmpMaxPort;
    this.#retentionMs = retentionMs;
    this.#maxSegmentsPerCall = maxSegmentsPerCall;
    this.#maxBytesPerCall = maxBytesPerCall;
    this.#maxSegmentBytes = maxSegmentBytes;
    this.#ffmpegPath = ffmpegPath;
    this.#startIngest = startIngest;
    this.#mediaPlane = mediaPlane;
    this.#tapAddress = tapAddress;
    this.#tapMinPort = tapMinPort;
    this.#tapMaxPort = tapMaxPort;
  }

  get ffmpegVersion() {
    const result = spawnSync(this.#ffmpegPath, ['-version'], {
      encoding: 'utf8', timeout: 2000
    });
    if (result.status !== 0) {
      return null;
    }
    return result.stdout.split(/\r?\n/, 1)[0].trim();
  }

  async createStream(callId, { rtmpStream = false } = {}) {
    safeCallId(callId);
    if (typeof rtmpStream !== 'boolean') {
      throw new TypeError('rtmpStream must be a boolean');
    }
    const existing = this.#calls.get(callId);
    if (existing) {
      if (existing.rtmpStream !== rtmpStream) {
        throw new BroadcastError('MODE_CONFLICT',
            'broadcast ingest mode cannot change for an active stream');
      }
      return;
    }
    const port = this.#allocatePort();
    const directory = join(this.#root, String(callId));
    await mkdir(directory, { recursive: true });
    const ring = new SegmentRing({
      root: join(directory, 'ring'),
      retentionMs: this.#retentionMs,
      maxSegments: this.#maxSegmentsPerCall,
      maxBytes: this.#maxBytesPerCall,
      maxSegmentBytes: this.#maxSegmentBytes
    });
    await ring.initialize();
    const call = {
      callId,
      rtmpStream,
      port,
      key: newStreamKey(),
      generation: 1,
      directory,
      ring,
      process: null,
      processDone: null,
      poller: null,
      indexedAudio: new Set(),
      indexedVideo: new Set(),
      epoch: 0,
      stopping: false,
      lastError: null,
      stderr: '',
      sourcePoller: null,
      keyFramePoller: null,
      sourceSyncing: false,
      sourceSignature: null,
      tapSubscriberId: `broadcast-${callId}`,
      tapPorts: [],
      endpointId: 'unified',
      activeMask: 1,
      publisherSessionId: null,
      indexing: null,
      vp9Bridge: null
    };
    this.#calls.set(callId, call);
    try {
      if (this.#startIngest && rtmpStream) {
        await this.#ensureRtmpServer();
      } else if (this.#startIngest && this.#mediaPlane) {
        this.#startSfuPolling(call);
      }
    } catch (error) {
      this.#calls.delete(callId);
      await ring.clear();
      await rm(directory, { recursive: true, force: true });
      throw error;
    }
  }

  async endStream(callId) {
    safeCallId(callId);
    const call = this.#calls.get(callId);
    if (!call) {
      return false;
    }
    if (call.publisherSessionId) {
      this.#rtmpServer?.getSession(call.publisherSessionId)?.reject();
      call.publisherSessionId = null;
    }
    this.#calls.delete(callId);
    call.stopping = true;
    if (call.sourcePoller) {
      clearInterval(call.sourcePoller);
      call.sourcePoller = null;
    }
    if (call.keyFramePoller) {
      clearInterval(call.keyFramePoller);
      call.keyFramePoller = null;
    }
    await this.#stopProcess(call);
    await call.ring.clear();
    await rm(call.directory, { recursive: true, force: true });
    return true;
  }

  async endAllStreams() {
    const callIds = [...this.#calls.keys()];
    for (const callId of callIds) {
      await this.endStream(callId);
    }
    return callIds.length;
  }

  async credentials(callId, revoke = false) {
    const call = this.#requireCall(callId);
    if (!call.rtmpStream) {
      throw new BroadcastError('MODE_CONFLICT',
          'ordinary calls do not have RTMP publisher credentials');
    }
    if (revoke) {
      if (call.publisherSessionId) {
        this.#rtmpServer?.getSession(call.publisherSessionId)?.reject();
        call.publisherSessionId = null;
      }
      call.key = newStreamKey();
      call.generation++;
      await this.#stopProcess(call);
      call.epoch = 0;
      call.indexedAudio.clear();
      call.indexedVideo.clear();
      await call.ring.clear();
      await call.ring.initialize();
    }
    return {
      url: `rtmp://${this.#rtmpAdvertisedAddress}:${call.port}/live`,
      key: call.key,
      generation: call.generation
    };
  }

  channels(callId) {
    return this.#requireCall(callId).ring.channels();
  }

  async readSegment(callId, { timestamp, scale, channel, quality = 0 }) {
    const call = this.#requireCall(callId);
    if (scale !== 0) {
      throw new BroadcastError('SCALE_UNSUPPORTED',
          'only one-second scale 0 segments are available');
    }
    const boundary = Math.floor(timestamp / SEGMENT_DURATION_MS) *
        SEGMENT_DURATION_MS;
    if (!Number.isSafeInteger(boundary) || boundary < 0) {
      throw new BroadcastError('TIME_INVALID', 'segment timestamp is invalid');
    }
    const resolvedQuality = this.#rendition.resolveQuality(quality);
    const result = await call.ring.read({
      timestamp: boundary,
      channel,
      quality: resolvedQuality
    });
    if (result) {
      return result;
    }
    if (call.ring.latestTimestamp === 0 || boundary > call.ring.latestTimestamp) {
      throw new BroadcastError('NOT_READY', 'segment is not ready');
    }
    throw new BroadcastError('SEGMENT_EXPIRED', 'segment is outside the live ring');
  }

  // Copy the already-decoded H264 output of the warm SFU tap into a
  // recording-owned directory one bounded segment at a time. A recording that
  // starts mid-call cannot safely create a second VP8 decoder: pinned group
  // tgcalls may keep sending interframes despite a late consumer PLI. Reusing
  // this continuous tap is both keyframe-safe and avoids another video router
  // consumer. The caller owns and eventually removes the copied files.
  async copyRecordingVideoSegments(callId, startedAtMs, directory,
      { waitMs = 3000, maxSegments = 14402 } = {}) {
    const call = this.#requireCall(callId);
    if (!Number.isSafeInteger(startedAtMs) || startedAtMs < 0 ||
        typeof directory !== 'string' || directory.length === 0 ||
        !Number.isInteger(waitMs) || waitMs < 0 ||
        !Number.isInteger(maxSegments) || maxSegments < 1) {
      throw new TypeError('recording video copy arguments are invalid');
    }
    const firstTimestamp = Math.ceil(startedAtMs / SEGMENT_DURATION_MS) *
      SEGMENT_DURATION_MS;
    const deadline = Date.now() + waitMs;
    let latestTimestamp = 0;
    do {
      // The public broadcast edge is deliberately the common completed
      // audio/video timestamp. Recording finalization copies only video, so an
      // audio muxer lag must not hide already completed video segments.
      latestTimestamp = call.ring.latestTimestampForChannel(1);
      if (latestTimestamp >= firstTimestamp) {
        break;
      }
      await new Promise((resolve) => setTimeout(resolve, 100));
    } while (Date.now() < deadline);
    // A segment can complete during the final sleep that crosses the deadline.
    // Observe the channel once more before classifying the tap as unavailable.
    latestTimestamp = Math.max(latestTimestamp,
        call.ring.latestTimestampForChannel(1));
    if (latestTimestamp < firstTimestamp) {
      const channels = call.ring.channels();
      let tap = null;
      try {
        tap = await this.#mediaPlane?.getRtpTapDiagnostics?.(
            call.callId, call.tapSubscriberId) ?? null;
      } catch (error) {
        tap = { diagnosticError: error?.message ?? String(error) };
      }
      const diagnostic = JSON.stringify({
        firstTimestamp,
        latestTimestamp,
        epoch: call.epoch,
        processRunning: Boolean(call.process),
        sourceAttached: call.sourceSignature !== null,
        channels,
        lastFailure: call.lastError,
        ffmpegDiagnostic: call.stderr || null,
        segmenter: await this.#readSegmenterState(call),
        tap
      });
      throw new BroadcastError('NOT_READY',
          `shared video segments are not ready for recording: ${diagnostic}`);
    }

    await mkdir(directory, { recursive: true });
    const names = [];
    for (let timestamp = firstTimestamp;
      timestamp <= latestTimestamp && names.length < maxSegments;
      timestamp += SEGMENT_DURATION_MS) {
      const wrapped = await call.ring.read({
        timestamp, channel: 1, quality: 2
      });
      if (!wrapped) {
        continue;
      }
      const parsed = parseVideoSegment(wrapped);
      if (!parsed || parsed.container !== 'mp4' || parsed.payload.length === 0) {
        throw new BroadcastError('UNAVAILABLE',
            'shared recording video segment is invalid');
      }
      const name = `video-${String(names.length).padStart(6, '0')}.mp4`;
      await writeFile(join(directory, name), parsed.payload, {
        flag: 'wx', mode: 0o600
      });
      names.push(name);
    }
    if (names.length === 0 ||
        latestTimestamp >= firstTimestamp + maxSegments * SEGMENT_DURATION_MS) {
      throw new BroadcastError('UNAVAILABLE',
          'shared recording video segment range is empty or exceeds its bound');
    }
    return names;
  }

  // Used by the SFU tap segmenter and deterministic adapter tests. It is not
  // exposed as a public unauthenticated route.
  async publishSegment(callId, { timestamp, channel, quality = 2, bytes,
    endpointId = 'unified', activeMask = 1, container = 'mp4' }) {
    const call = this.#requireCall(callId);
    const payload = channel === 0 ? bytes : wrapVideoSegment({
      container,
      endpointId,
      activeMask,
      media: bytes
    });
    await call.ring.put({ timestamp, channel, quality, bytes: payload });
  }

  async health() {
    const ffmpegVersion = this.#startIngest ? this.ffmpegVersion : 'test-disabled';
    const failed = [...this.#calls.values()].find((call) => call.lastError);
    const diagnostic = failed ?? [...this.#calls.values()].find((call) =>
      call.stderr.length > 0);
    return {
      healthy: !this.#startIngest ||
        (ffmpegVersion !== null && this.#rtmpServerError === null),
      streams: this.#calls.size,
      liveStreams: [...this.#calls.values()].filter((call) =>
        call.ring.latestTimestamp !== 0).length,
      segments: [...this.#calls.values()].reduce((sum, call) =>
        sum + call.ring.count, 0),
      bytes: [...this.#calls.values()].reduce((sum, call) =>
        sum + call.ring.bytes, 0),
      ffmpegVersion,
      lastError: failed?.lastError ?? null,
      ffmpegDiagnostic: diagnostic?.stderr ?? null
    };
  }

  diagnostics(callId) {
    const call = this.#requireCall(callId);
    return {
      mode: call.rtmpStream ? 'rtmp' : 'sfu',
      processRunning: Boolean(call.process),
      sourceAttached: call.sourceSignature !== null,
      vp9Bridge: call.vp9Bridge
        ? {
            packets: call.vp9Bridge.packets,
            parsed: call.vp9Bridge.parsed,
            ignoredLayers: call.vp9Bridge.ignoredLayers,
            frames: call.vp9Bridge.frames,
            dropped: call.vp9Bridge.dropped
          }
        : null,
      lastError: call.lastError,
      stderr: call.stderr
    };
  }

  async refreshSfu(callId) {
    const call = this.#requireCall(callId);
    if (call.rtmpStream || !this.#mediaPlane) {
      return false;
    }
    const deadline = Date.now() + 3000;
    do {
      while (call.sourceSyncing && Date.now() < deadline) {
        await new Promise((resolve) => setTimeout(resolve, 25));
      }
      await this.#syncSfu(call);
      if (call.process && call.sourceSignature !== null) {
        return true;
      }
      await new Promise((resolve) => setTimeout(resolve, 50));
    } while (Date.now() < deadline);
    return false;
  }

  async close() {
    this.#stopping = true;
    await this.endAllStreams();
    if (this.#rtmpListeners) {
      for (const [event, listener] of this.#rtmpListeners) {
        nodeMediaContext.nodeEvent.off(event, listener);
      }
      this.#rtmpListeners = null;
    }
    this.#rtmpServer?.stop();
    this.#rtmpServer = null;
    this.#rtmpReady = null;
  }

  #allocatePort() {
    const capacity = this.#rtmpMaxPort - this.#rtmpMinPort + 1;
    const allocated = [...this.#calls.values()].filter((call) =>
      call.rtmpStream).length;
    if (allocated < capacity) {
      // One authenticated RTMP server multiplexes random per-call stream keys.
      // The configured range remains the explicit call-capacity bound while
      // only its first port needs public exposure.
      return this.#rtmpMinPort;
    }
    throw new BroadcastError('CAPACITY', 'RTMP listener capacity is exhausted');
  }

  #allocateTapPorts(count) {
    // Reserve the adjacent RTCP port even though mediasoup sends rtcp-mux.
    // FFmpeg's SDP demuxer still opens RTP+1 while initializing each media
    // section, so consecutive RTP targets collide before it applies rtcp-mux.
    const used = new Set([...this.#calls.values()]
        .flatMap((call) => call.tapPorts.flatMap((port) => [port, port + 1])));
    const result = [];
    for (let port = this.#tapMinPort;
      port + 1 <= this.#tapMaxPort && result.length < count;
      port++) {
      if (!used.has(port) && !used.has(port + 1)) {
        result.push(port);
        used.add(port);
        used.add(port + 1);
      }
    }
    if (result.length !== count) {
      throw new BroadcastError('CAPACITY',
          'RTP tap listener capacity is exhausted');
    }
    return result;
  }

  #requireCall(callId) {
    safeCallId(callId);
    const call = this.#calls.get(callId);
    if (!call) {
      throw new BroadcastError('STREAM_MISSING', `stream ${callId} is missing`);
    }
    return call;
  }

  async #ensureRtmpServer() {
    if (this.#rtmpServer) {
      await this.#rtmpReady;
      if (this.#rtmpServerError) {
        throw new BroadcastError('UNAVAILABLE', this.#rtmpServerError);
      }
      return;
    }
    const config = {
      logType: 0,
      rtmp: {
        port: this.#rtmpMinPort,
        chunk_size: 60_000,
        gop_cache: true,
        ping: 30,
        ping_timeout: 30
      }
    };
    const server = new NodeMediaServer(config);
    nodeMediaLogger.setLogType(0);
    const rtmp = new NodeRtmpServer(config);
    server.nrs = rtmp;
    this.#rtmpServerError = null;

    const prePublish = (id, streamPath) => {
      if (!this.#callForStreamPath(streamPath)) {
        server.getSession(id)?.reject();
      }
    };
    const postPublish = (id, streamPath) => {
      const call = this.#callForStreamPath(streamPath);
      if (!call || call.publisherSessionId) {
        server.getSession(id)?.reject();
        return;
      }
      call.publisherSessionId = id;
      this.#spawn(call);
    };
    const donePublish = (id, streamPath) => {
      const call = this.#callForStreamPath(streamPath);
      if (!call || call.publisherSessionId !== id) {
        return;
      }
      call.publisherSessionId = null;
    };
    this.#rtmpListeners = [
      ['prePublish', prePublish],
      ['postPublish', postPublish],
      ['donePublish', donePublish]
    ];
    for (const [event, listener] of this.#rtmpListeners) {
      nodeMediaContext.nodeEvent.on(event, listener);
    }
    this.#rtmpServer = server;
    this.#rtmpReady = new Promise((resolve, reject) => {
      const startupError = (error) => {
        this.#rtmpServerError = `RTMP server failed: ${error.message}`;
        reject(new BroadcastError('UNAVAILABLE', this.#rtmpServerError));
      };
      rtmp.tcpServer.once('error', startupError);
      rtmp.tcpServer.once('listening', () => {
        rtmp.tcpServer.off('error', startupError);
        rtmp.tcpServer.on('error', (error) => {
          this.#rtmpServerError = `RTMP server failed: ${error.message}`;
        });
        resolve();
      });
      rtmp.tcpServer.listen(this.#rtmpMinPort, this.#rtmpBindAddress);
    });
    try {
      await this.#rtmpReady;
    } catch (error) {
      for (const [event, listener] of this.#rtmpListeners) {
        nodeMediaContext.nodeEvent.off(event, listener);
      }
      this.#rtmpListeners = null;
      if (rtmp.tcpServer.listening) {
        rtmp.stop();
      }
      this.#rtmpServer = null;
      this.#rtmpReady = null;
      throw error;
    }
  }

  #callForStreamPath(streamPath) {
    if (typeof streamPath !== 'string') {
      return null;
    }
    return [...this.#calls.values()].find((call) => call.rtmpStream &&
      !call.stopping && streamPath === `/live/${call.key}`) ?? null;
  }

  #spawn(call) {
    if (call.stopping || this.#stopping || call.process ||
        !call.publisherSessionId) {
      return;
    }
    const metadata = encodeAudioMetadata({
      channelCount: 1,
      updates: [{ frameIndex: 0, channelId: 0, ssrc: 1 }]
    });
    const args = buildRtmpFfmpegArguments({
      bindAddress: this.#rtmpBindAddress,
      port: call.port,
      key: call.key,
      inputUrl: `rtmp://127.0.0.1:${call.port}/live/${call.key}`,
      directory: call.directory,
      audioMetadata: metadata
    });
    const child = spawn(this.#ffmpegPath, args, {
      stdio: ['ignore', 'ignore', 'pipe']
    });
    call.process = child;
    let resolveProcessDone;
    call.processDone = new Promise((resolve) => {
      resolveProcessDone = resolve;
    });
    call.epoch = Math.floor(Date.now() / 1000) * 1000;
    call.stderr = '';
    child.stderr.on('data', (chunk) => {
      call.stderr = (call.stderr + chunk.toString('utf8')).slice(-8192);
    });
    call.poller = setInterval(() => {
      this.#indexCompleted(call).catch((error) => {
        call.lastError = error?.message ?? String(error);
      });
    }, 200);
    call.poller.unref();
    child.once('error', (error) => {
      call.lastError = error.message;
    });
    child.once('exit', async () => {
      try {
        if (call.process !== child) {
          return;
        }
        if (call.poller) {
          clearInterval(call.poller);
          call.poller = null;
        }
        try {
          await this.#indexCompleted(call, true);
        } catch (error) {
          call.lastError = error?.message ?? String(error);
        }
        call.process = null;
        if (!call.stopping && !this.#stopping && call.publisherSessionId) {
          const timer = setTimeout(() => this.#spawn(call), 500);
          timer.unref();
        }
      } finally {
        resolveProcessDone();
      }
    });
  }

  #startSfuPolling(call) {
    const sync = () => this.#syncSfu(call).catch((error) => {
      call.lastError = error?.message ?? String(error);
    });
    void sync();
    call.sourcePoller = setInterval(sync, 500);
    call.sourcePoller.unref();
  }

  async #syncSfu(call) {
    if (call.sourceSyncing || call.stopping || this.#stopping ||
        !this.#calls.has(call.callId)) {
      return;
    }
    call.sourceSyncing = true;
    try {
      const sources = this.#mediaPlane.getRtpTapSources(call.callId);
      const video = sources
          .filter((source) => source.kind === 'video')
          .sort((left, right) => Number(right.presentation) -
            Number(left.presentation))[0];
      const audio = sources.find((source) => source.kind === 'audio' &&
        source.participantId === video?.participantId) ??
        sources.find((source) => source.kind === 'audio');
      if (!audio || !video) {
        if (call.sourceSignature !== null) {
          await this.#stopProcess(call);
          call.sourceSignature = null;
        }
        return;
      }
      const selected = [audio, video];
      const signature = JSON.stringify(selected.map((source) => [
        source.participantId, source.presentation, source.kind, source.endpoint,
        source.codec
      ]));
      if (signature === call.sourceSignature && call.process) {
        return;
      }

      await this.#stopProcess(call);
      call.sourceSignature = signature;
      call.tapPorts = this.#allocateTapPorts(selected.length);
      const targets = selected.map((source, index) => ({
        participantId: source.participantId,
        presentation: source.presentation,
        kind: source.kind,
        ip: this.#tapAddress,
        port: call.tapPorts[index],
        paused: true
      }));
      const vp9 = video.codec?.toLowerCase() === 'video/vp9';
      try {
        if (vp9) {
          const videoTarget = targets.find((target) => target.kind === 'video');
          if (!videoTarget) {
            throw new Error('VP9 RTP tap has no video target');
          }
          call.vp9Bridge = new Vp9RtpIvfBridge();
          await call.vp9Bridge.bind(this.#tapAddress, videoTarget.port);
        }
        let descriptors = await this.#mediaPlane.acquireRtpTap(call.callId,
            call.tapSubscriberId, targets);
        descriptors = descriptors.map((descriptor, index) => ({
          ...descriptor,
          targetPort: call.tapPorts[index]
        }));
        await writeFile(join(call.directory, 'tap.sdp'),
            buildRtpTapSdp(vp9
              ? descriptors.filter((descriptor) => descriptor.kind !== 'video')
              : descriptors), { encoding: 'utf8' });
        await this.#spawnSfu(call, descriptors, vp9);
      } catch (error) {
        await call.vp9Bridge?.close();
        call.vp9Bridge = null;
        this.#mediaPlane.releaseRtpTap(call.callId, call.tapSubscriberId);
        call.tapPorts = [];
        call.sourceSignature = null;
        throw error;
      }
    } finally {
      call.sourceSyncing = false;
    }
  }

  async #spawnSfu(call, descriptors, vp9Pipe) {
    const audio = descriptors.find((descriptor) => descriptor.kind === 'audio');
    const video = descriptors.find((descriptor) => descriptor.kind === 'video');
    const audioSsrc = audio?.rtpParameters?.encodings?.[0]?.ssrc ?? 1;
    const endpointId = video?.endpoint ?? 'unified';
    const activeMask = video ? 1 : 0;
    const metadata = encodeAudioMetadata({
      channelCount: 1,
      updates: [{ frameIndex: 0, channelId: 0, ssrc: audioSsrc | 0 }]
    });
    const args = buildRtpTapFfmpegArguments({
      sdpPath: join(call.directory, 'tap.sdp'),
      directory: call.directory,
      audioMetadata: metadata,
      activeMask,
      endpoints: endpointId,
      vp9Pipe
    });
    const child = spawn(this.#ffmpegPath, args, {
      stdio: vp9Pipe
        ? ['ignore', 'ignore', 'pipe', 'pipe']
        : ['ignore', 'ignore', 'pipe']
    });
    if (vp9Pipe) {
      call.vp9Bridge.attach(child.stdio[3]);
    }
    call.process = child;
    let resolveProcessDone;
    call.processDone = new Promise((resolve) => {
      resolveProcessDone = resolve;
    });
    call.epoch = Math.floor(Date.now() / 1000) * 1000;
    call.endpointId = endpointId;
    call.activeMask = activeMask;
    call.indexedAudio.clear();
    call.indexedVideo.clear();
    call.stderr = '';
    child.stderr.on('data', (chunk) => {
      call.stderr = (call.stderr + chunk.toString('utf8')).slice(-8192);
    });
    call.poller = setInterval(() => {
      this.#indexCompleted(call).catch((error) => {
        call.lastError = error?.message ?? String(error);
      });
    }, 200);
    call.poller.unref();
    child.once('error', (error) => {
      call.lastError = error.message;
    });
    child.once('exit', async () => {
      try {
        if (call.process !== child) {
          return;
        }
        if (call.poller) {
          clearInterval(call.poller);
          call.poller = null;
        }
        try {
          await this.#indexCompleted(call, true);
        } catch (error) {
          call.lastError = error?.message ?? String(error);
        }
        call.process = null;
        await call.vp9Bridge?.close();
        call.vp9Bridge = null;
        this.#mediaPlane.releaseRtpTap(call.callId, call.tapSubscriberId);
        call.tapPorts = [];
        call.sourceSignature = null;
      } finally {
        resolveProcessDone();
      }
    });
    await new Promise((resolve) => setTimeout(resolve, 250));
    if (call.process !== child || call.stopping || this.#stopping) {
      return;
    }
    await this.#mediaPlane.resumeRtpTap(call.callId, call.tapSubscriberId);
    const requestKeyFrame = () => {
      if (call.stopping || this.#stopping || !call.process ||
          call.ring.latestTimestamp !== 0) {
        if (call.keyFramePoller) {
          clearInterval(call.keyFramePoller);
          call.keyFramePoller = null;
        }
        return;
      }
      this.#mediaPlane.requestRtpTapKeyFrames(call.callId,
          call.tapSubscriberId).catch((error) => {
        call.lastError = error?.message ?? String(error);
      });
    };
    call.keyFramePoller = setInterval(requestKeyFrame, 500);
    call.keyFramePoller.unref();
    requestKeyFrame();
  }

  async #stopProcess(call) {
    if (call.poller) {
      clearInterval(call.poller);
      call.poller = null;
    }
    if (call.keyFramePoller) {
      clearInterval(call.keyFramePoller);
      call.keyFramePoller = null;
    }
    if (call.process) {
      const child = call.process;
      const done = call.processDone;
      call.process = null;
      child.kill('SIGTERM');
      if (done) {
        let killTimer;
        await Promise.race([
          done,
          new Promise((resolve) => {
            killTimer = setTimeout(() => {
              child.kill('SIGKILL');
              resolve();
            }, 2000);
            killTimer.unref();
          })
        ]);
        clearTimeout(killTimer);
      }
      call.processDone = null;
    }
    if (call.indexing) {
      await call.indexing;
    }
    await call.vp9Bridge?.close();
    call.vp9Bridge = null;
    if (!call.rtmpStream && this.#mediaPlane) {
      this.#mediaPlane.releaseRtpTap(call.callId, call.tapSubscriberId);
      call.tapPorts = [];
    }
  }

  async #indexCompleted(call, includeFinal = false) {
    if (!includeFinal && call.indexing) {
      return call.indexing;
    }
    while (call.indexing) {
      await call.indexing;
    }
    const operation = (async () => {
      await this.#indexList(call, 'audio.csv', call.indexedAudio, 0, false,
          includeFinal);
      await this.#indexList(call, 'video.csv', call.indexedVideo, 1, true,
          includeFinal);
    })();
    call.indexing = operation;
    try {
      await operation;
    } finally {
      if (call.indexing === operation) {
        call.indexing = null;
      }
    }
  }

  // A frozen video edge is either FFmpeg no longer completing segments or the
  // indexer no longer publishing the ones on disk. Only the segmenter's own
  // output separates the two, so report it alongside the ring's view.
  async #readSegmenterState(call) {
    const state = {
      videoListEntries: 0,
      lastVideoEntry: null,
      indexedVideo: call.indexedVideo?.size ?? 0,
      unindexedFiles: 0
    };
    try {
      const entries = parseCompletedList(
          await readFile(join(call.directory, 'video.csv'), 'utf8'));
      state.videoListEntries = entries.length;
      state.lastVideoEntry = entries.at(-1) ?? null;
    } catch (error) {
      if (error?.code !== 'ENOENT') {
        state.listError = error?.message ?? String(error);
      }
    }
    try {
      const files = await readdir(call.directory);
      state.unindexedFiles = files.filter((name) =>
        /^video-\d+\.mp4$/.test(name)).length;
    } catch (error) {
      state.directoryError = error?.message ?? String(error);
    }
    return state;
  }

  async #indexList(call, listName, indexed, channel, video, includeFinal) {
    let value;
    try {
      value = await readFile(join(call.directory, listName), 'utf8');
    } catch (error) {
      if (error?.code === 'ENOENT') {
        return;
      }
      throw error;
    }
    const entries = parseCompletedList(value);
    // FFmpeg's current last list entry can still be open. Index every earlier
    // entry; after process exit/restart the final complete file is picked up by
    // the next pass only if another entry exists, avoiding partial publication.
    for (const entry of includeFinal ? entries : entries.slice(0, -1)) {
      if (indexed.has(entry.file)) {
        continue;
      }
      const match = /-(\d+)\.(?:ogg|mp4)$/.exec(entry.file);
      if (!match) {
        continue;
      }
      const index = Number(match[1]);
      const timestamp = call.epoch + index * SEGMENT_DURATION_MS;
      const media = await readFile(join(call.directory, entry.file));
      await call.ring.put({
        timestamp,
        channel,
        quality: 2,
        bytes: video ? wrapVideoSegment({
          endpointId: call.endpointId,
          activeMask: call.activeMask,
          media
        }) : media
      });
      indexed.add(entry.file);
      await rm(join(call.directory, entry.file), { force: true });
    }
  }
}
