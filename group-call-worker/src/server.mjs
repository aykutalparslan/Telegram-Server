// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

import { GroupCallMediaPlane } from './media-plane.mjs';
import { createGroupCallControlServer } from './control-server.mjs';
import { GroupCallBroadcastService } from './broadcast-service.mjs';
import { GroupCallRecordingService } from './recording-service.mjs';

function integer(name, fallback) {
  const text = process.env[name];
  if (text === undefined || text.length === 0) {
    return fallback;
  }
  const value = Number(text);
  if (!Number.isInteger(value)) {
    throw new TypeError(`${name} must be an integer`);
  }
  return value;
}

const authSecret = process.env.FERRITE_GROUP_CALL_AUTH_SECRET;
if (!authSecret) {
  throw new Error('FERRITE_GROUP_CALL_AUTH_SECRET is required');
}

const plane = new GroupCallMediaPlane({
  listenIp: process.env.FERRITE_GROUP_CALL_MEDIA_BIND ?? '0.0.0.0',
  announcedAddress: process.env.FERRITE_GROUP_CALL_MEDIA_ADVERTISED,
  rtcMinPort: integer('FERRITE_GROUP_CALL_RTC_MIN_PORT', 40000),
  rtcMaxPort: integer('FERRITE_GROUP_CALL_RTC_MAX_PORT', 40100),
  maxRooms: integer('FERRITE_GROUP_CALL_MAX_ROOMS', 100),
  maxParticipantsPerRoom: integer(
      'FERRITE_GROUP_CALL_MAX_PARTICIPANTS_PER_ROOM', 1000)
});
await plane.start();

const broadcast = new GroupCallBroadcastService({
  root: process.env.FERRITE_GROUP_CALL_SEGMENT_PATH ?? '/tmp/ferrite-broadcast',
  rtmpBindAddress: process.env.FERRITE_GROUP_CALL_RTMP_BIND ?? '0.0.0.0',
  rtmpAdvertisedAddress: process.env.FERRITE_GROUP_CALL_RTMP_ADVERTISED ??
      process.env.FERRITE_GROUP_CALL_MEDIA_ADVERTISED ?? '127.0.0.1',
  rtmpMinPort: integer('FERRITE_GROUP_CALL_RTMP_MIN_PORT', 19350),
  rtmpMaxPort: integer('FERRITE_GROUP_CALL_RTMP_MAX_PORT', 19449),
  retentionMs: integer('FERRITE_GROUP_CALL_SEGMENT_RETENTION_MS', 300000),
  maxSegmentsPerCall: integer(
      'FERRITE_GROUP_CALL_MAX_SEGMENTS_PER_CALL', 4096),
  maxBytesPerCall: integer(
      'FERRITE_GROUP_CALL_MAX_SEGMENT_BYTES_PER_CALL', 512 * 1024 * 1024),
  maxSegmentBytes: integer('FERRITE_GROUP_CALL_MAX_SEGMENT_BYTES', 1024 * 1024),
  mediaPlane: plane,
  tapAddress: process.env.FERRITE_GROUP_CALL_RTP_TAP_ADDRESS ?? '127.0.0.1',
  tapMinPort: integer('FERRITE_GROUP_CALL_RTP_TAP_MIN_PORT', 50000),
  tapMaxPort: integer('FERRITE_GROUP_CALL_RTP_TAP_MAX_PORT', 50199)
});

const recording = new GroupCallRecordingService({
  root: process.env.FERRITE_GROUP_CALL_RECORDING_PATH ?? '/recordings',
  mediaPlane: plane,
  broadcastService: broadcast,
  tapAddress: process.env.FERRITE_GROUP_CALL_RTP_TAP_ADDRESS ?? '127.0.0.1',
  tapMinPort: integer('FERRITE_GROUP_CALL_RECORDING_TAP_MIN_PORT', 50200),
  tapMaxPort: integer('FERRITE_GROUP_CALL_RECORDING_TAP_MAX_PORT', 50399),
  maxRecordings: integer('FERRITE_GROUP_CALL_MAX_RECORDINGS', 16),
  maxBytes: integer('FERRITE_GROUP_CALL_MAX_RECORDING_BYTES', 2_000_000_000),
  maxDurationMs: integer('FERRITE_GROUP_CALL_MAX_RECORDING_DURATION_MS',
      4 * 60 * 60 * 1000),
  sourceWaitMs: integer('FERRITE_GROUP_CALL_RECORDING_SOURCE_WAIT_MS', 30_000),
  stopTimeoutMs: integer('FERRITE_GROUP_CALL_RECORDING_STOP_TIMEOUT_MS', 10_000)
});

const control = createGroupCallControlServer({
  plane,
  broadcast,
  recording,
  authSecret,
  maxBodyBytes: integer('FERRITE_GROUP_CALL_MAX_CONTROL_BODY_BYTES',
      256 * 1024),
  maxEventClients: integer('FERRITE_GROUP_CALL_MAX_EVENT_CLIENTS', 32)
});
const address = await control.listen({
  host: process.env.FERRITE_GROUP_CALL_CONTROL_BIND ?? '127.0.0.1',
  port: integer('FERRITE_GROUP_CALL_CONTROL_PORT', 9090)
});
console.log(`Ferrite group-call worker listening on ${address.address}:${address.port}`);

let stopping = false;
async function stop() {
  if (stopping) {
    return;
  }
  stopping = true;
  await control.close();
}
process.once('SIGTERM', () => void stop());
process.once('SIGINT', () => void stop());
