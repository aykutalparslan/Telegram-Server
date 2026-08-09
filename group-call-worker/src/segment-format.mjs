// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

const VIDEO_SIGNATURE = 0xa12e810d;
const MAX_SERIALIZED_STRING_BYTES = 0xffffff;
const MAX_CHANNEL_UPDATES = 4096;

function requireInt32(value, name) {
  if (!Number.isInteger(value) || value < -0x80000000 ||
      value > 0x7fffffff) {
    throw new TypeError(`${name} must be a signed int32`);
  }
  return value;
}

function requireUInt32(value, name) {
  if (!Number.isInteger(value) || value < 0 || value > 0xffffffff) {
    throw new TypeError(`${name} must be an unsigned int32`);
  }
  return value;
}

export function telegramSerializedString(value) {
  if (typeof value !== 'string') {
    throw new TypeError('serialized value must be a string');
  }
  const bytes = Buffer.from(value, 'utf8');
  if (bytes.length > MAX_SERIALIZED_STRING_BYTES) {
    throw new RangeError('serialized string exceeds the Telegram byte limit');
  }

  const prefixLength = bytes.length < 254 ? 1 : 4;
  const paddedLength = Math.ceil((prefixLength + bytes.length) / 4) * 4;
  const result = Buffer.alloc(paddedLength);
  if (prefixLength === 1) {
    result[0] = bytes.length;
  } else {
    result[0] = 254;
    result[1] = bytes.length & 0xff;
    result[2] = (bytes.length >>> 8) & 0xff;
    result[3] = (bytes.length >>> 16) & 0xff;
  }
  bytes.copy(result, prefixLength);
  return result;
}

function readInt32(buffer, state) {
  if (state.offset + 4 > buffer.length) {
    return null;
  }
  const value = buffer.readInt32LE(state.offset);
  state.offset += 4;
  return value;
}

function readSerializedString(buffer, state) {
  if (state.offset >= buffer.length) {
    return null;
  }
  const first = buffer[state.offset++];
  let length;
  let prefixLength;
  if (first === 254) {
    if (state.offset + 3 > buffer.length) {
      return null;
    }
    length = buffer[state.offset] |
      (buffer[state.offset + 1] << 8) |
      (buffer[state.offset + 2] << 16);
    state.offset += 3;
    prefixLength = 4;
  } else {
    length = first;
    prefixLength = 1;
  }
  if (state.offset + length > buffer.length) {
    return null;
  }
  const value = buffer.subarray(state.offset, state.offset + length)
      .toString('utf8');
  state.offset += length;
  const padding = (4 - ((prefixLength + length) % 4)) % 4;
  if (state.offset + padding > buffer.length) {
    return null;
  }
  state.offset += padding;
  return value;
}

// This is the exact framing consumed by the pinned
// VideoStreamingPart.cpp::consumeVideoStreamInfo. A video request names one
// channel, so production emits one event and a payload-relative offset of zero.
export function wrapVideoSegment({
  container = 'mp4',
  activeMask = 1,
  endpointId,
  rotation = 0,
  extra = 0,
  media
}) {
  if (typeof endpointId !== 'string' || endpointId.length === 0) {
    throw new TypeError('video endpointId must not be empty');
  }
  requireInt32(activeMask, 'video activeMask');
  requireInt32(rotation, 'video rotation');
  requireInt32(extra, 'video extra');
  if (!Buffer.isBuffer(media) || media.length === 0) {
    throw new TypeError('video media must be a non-empty Buffer');
  }

  const containerBytes = telegramSerializedString(container);
  const endpointBytes = telegramSerializedString(endpointId);
  const header = Buffer.alloc(4 + containerBytes.length + 4 + 4 + 4 +
      endpointBytes.length + 4 + 4);
  let offset = 0;
  header.writeUInt32LE(VIDEO_SIGNATURE, offset);
  offset += 4;
  containerBytes.copy(header, offset);
  offset += containerBytes.length;
  header.writeInt32LE(activeMask, offset);
  offset += 4;
  header.writeInt32LE(1, offset);
  offset += 4;
  header.writeInt32LE(0, offset);
  offset += 4;
  endpointBytes.copy(header, offset);
  offset += endpointBytes.length;
  header.writeInt32LE(rotation, offset);
  offset += 4;
  header.writeInt32LE(extra, offset);

  return Buffer.concat([header, media]);
}

// Behavioral port of the pinned client parser. It deliberately consumes one
// event when eventCount is positive because that is what layer-214 tgcalls does.
export function parseVideoSegment(buffer) {
  if (!Buffer.isBuffer(buffer)) {
    throw new TypeError('video segment must be a Buffer');
  }
  const state = { offset: 0 };
  const signature = readInt32(buffer, state);
  if (signature === null || (signature >>> 0) !== VIDEO_SIGNATURE) {
    return null;
  }
  const container = readSerializedString(buffer, state);
  const activeMask = readInt32(buffer, state);
  const eventCount = readInt32(buffer, state);
  if (container === null || activeMask === null || eventCount === null ||
      eventCount <= 0) {
    return null;
  }
  const offset = readInt32(buffer, state);
  const endpointId = readSerializedString(buffer, state);
  const rotation = readInt32(buffer, state);
  const extra = readInt32(buffer, state);
  if (offset === null || endpointId === null || rotation === null ||
      extra === null) {
    return null;
  }
  const payload = buffer.subarray(state.offset);
  if (offset < 0 || offset >= payload.length) {
    return null;
  }
  return {
    container,
    activeMask,
    eventCount,
    events: [{ offset, endpointId, rotation, extra }],
    payload
  };
}

export function encodeAudioMetadata({ channelCount, updates }) {
  if (!Number.isInteger(channelCount) || channelCount < 1 ||
      channelCount > 8) {
    throw new RangeError('audio channelCount must be between 1 and 8');
  }
  if (!Array.isArray(updates) || updates.length === 0 ||
      updates.length > MAX_CHANNEL_UPDATES) {
    throw new RangeError('audio updates must contain 1..4096 entries');
  }
  const result = Buffer.alloc(8 + updates.length * 12);
  result.writeInt32LE(channelCount, 0);
  result.writeInt32LE(updates.length, 4);
  let offset = 8;
  for (const update of updates) {
    requireInt32(update.frameIndex, 'audio update frameIndex');
    requireInt32(update.channelId, 'audio update channelId');
    requireUInt32(update.ssrc, 'audio update ssrc');
    result.writeInt32LE(update.frameIndex, offset);
    result.writeInt32LE(update.channelId, offset + 4);
    result.writeUInt32LE(update.ssrc, offset + 8);
    offset += 12;
  }
  return result.toString('base64');
}

export function parseAudioMetadata(value) {
  if (typeof value !== 'string' || value.length === 0) {
    return null;
  }
  const buffer = Buffer.from(value, 'base64');
  if (buffer.length < 8) {
    return null;
  }
  const channelCount = buffer.readInt32LE(0);
  const count = buffer.readInt32LE(4);
  if (channelCount < 1 || channelCount > 8 || count < 0 ||
      count > MAX_CHANNEL_UPDATES || buffer.length !== 8 + count * 12) {
    return null;
  }
  const updates = [];
  for (let index = 0; index < count; index++) {
    const offset = 8 + index * 12;
    updates.push({
      frameIndex: buffer.readInt32LE(offset),
      channelId: buffer.readInt32LE(offset + 4),
      ssrc: buffer.readUInt32LE(offset + 8)
    });
  }
  return { channelCount, updates };
}

export { VIDEO_SIGNATURE };
