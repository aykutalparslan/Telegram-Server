// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

import { createSocket } from 'node:dgram';

function requireBytes(value, offset, count) {
  if (offset + count > value.length) {
    throw new TypeError('truncated VP9 RTP payload descriptor');
  }
}

// RFC 7741 payload-descriptor parsing. The scalability structure is consumed
// here instead of by FFmpeg because FFmpeg 7.x cannot depacketize Telegram's
// multi-spatial-layer VP9 descriptors even after mediasoup selects one layer.
export function parseVp9Payload(value) {
  if (!Buffer.isBuffer(value) || value.length < 2) {
    throw new TypeError('VP9 RTP payload is empty');
  }
  let offset = 0;
  const flags = value[offset++];
  const pictureId = Boolean(flags & 0x80);
  const predicted = Boolean(flags & 0x40);
  const layerInfo = Boolean(flags & 0x20);
  const flexible = Boolean(flags & 0x10);
  const beginning = Boolean(flags & 0x08);
  const end = Boolean(flags & 0x04);
  const scalability = Boolean(flags & 0x02);
  let spatialLayer = 0;
  let temporalLayer = 0;
  let width = 0;
  let height = 0;

  if (pictureId) {
    requireBytes(value, offset, 1);
    const extended = Boolean(value[offset++] & 0x80);
    if (extended) {
      requireBytes(value, offset, 1);
      offset++;
    }
  }
  if (layerInfo) {
    requireBytes(value, offset, 1);
    const layer = value[offset++];
    temporalLayer = layer >>> 5;
    spatialLayer = (layer >>> 1) & 0x07;
    if (!flexible) {
      requireBytes(value, offset, 1);
      offset++;
    }
  }
  if (predicted && flexible) {
    let more;
    do {
      requireBytes(value, offset, 1);
      more = Boolean(value[offset++] & 0x01);
    } while (more);
  }
  if (scalability) {
    requireBytes(value, offset, 1);
    const structure = value[offset++];
    const spatialLayers = (structure >>> 5) + 1;
    const resolutions = Boolean(structure & 0x10);
    const groups = Boolean(structure & 0x08);
    if (resolutions) {
      requireBytes(value, offset, spatialLayers * 4);
      for (let index = 0; index < spatialLayers; index++) {
        const layerWidth = value.readUInt16BE(offset);
        const layerHeight = value.readUInt16BE(offset + 2);
        if (index === spatialLayer) {
          width = layerWidth;
          height = layerHeight;
        }
        offset += 4;
      }
    }
    if (groups) {
      requireBytes(value, offset, 1);
      const groupCount = value[offset++];
      for (let index = 0; index < groupCount; index++) {
        requireBytes(value, offset, 1);
        const references = (value[offset++] >>> 2) & 0x03;
        requireBytes(value, offset, references);
        offset += references;
      }
    }
  }
  requireBytes(value, offset, 1);
  return {
    beginning,
    end,
    spatialLayer,
    temporalLayer,
    width,
    height,
    payload: value.subarray(offset)
  };
}

export function parseRtpPacket(value) {
  if (!Buffer.isBuffer(value) || value.length < 12 || value[0] >>> 6 !== 2 ||
      (value[1] >= 200 && value[1] <= 207)) {
    return null;
  }
  const padding = Boolean(value[0] & 0x20);
  const extension = Boolean(value[0] & 0x10);
  const sources = value[0] & 0x0f;
  let offset = 12 + sources * 4;
  if (extension) {
    if (offset + 4 > value.length) {
      return null;
    }
    offset += 4 + value.readUInt16BE(offset + 2) * 4;
  }
  let end = value.length;
  if (padding) {
    const paddingBytes = value[value.length - 1];
    if (paddingBytes === 0 || paddingBytes > end - offset) {
      return null;
    }
    end -= paddingBytes;
  }
  if (offset >= end) {
    return null;
  }
  return {
    sequence: value.readUInt16BE(2),
    timestamp: value.readUInt32BE(4),
    payload: value.subarray(offset, end)
  };
}

function ivfHeader(width, height) {
  const header = Buffer.alloc(32);
  header.write('DKIF', 0, 'ascii');
  header.writeUInt16LE(0, 4);
  header.writeUInt16LE(32, 6);
  header.write('VP90', 8, 'ascii');
  header.writeUInt16LE(width || 640, 12);
  header.writeUInt16LE(height || 360, 14);
  // Pinned tgcalls' L3T3 base temporal layer produces two frames per second.
  // IVF stores a nominal frame rate here; RTP's 90 kHz clock belongs only in
  // the depacketizer and would make FFmpeg synthesize 90,000 output frames/s.
  header.writeUInt32LE(2, 16);
  header.writeUInt32LE(1, 20);
  return header;
}

function ivfFrame(payload, timestamp) {
  const header = Buffer.alloc(12);
  header.writeUInt32LE(payload.length, 0);
  header.writeBigUInt64LE(BigInt(timestamp), 4);
  return Buffer.concat([header, payload]);
}

export class Vp9RtpIvfBridge {
  #socket = null;
  #writer = null;
  #frame = null;
  #headerWritten = false;
  #backpressured = false;
  #closed = false;

  constructor({ spatialLayer = 0, temporalLayer = 0 } = {}) {
    this.spatialLayer = spatialLayer;
    this.temporalLayer = temporalLayer;
    this.frames = 0;
    this.dropped = 0;
    this.packets = 0;
    this.parsed = 0;
    this.ignoredLayers = 0;
  }

  async bind(address, port) {
    if (this.#socket) {
      throw new Error('VP9 RTP bridge is already bound');
    }
    const socket = createSocket('udp4');
    this.#socket = socket;
    socket.on('message', (packet) => this.#receive(packet));
    await new Promise((resolve, reject) => {
      const error = (value) => {
        socket.off('listening', listening);
        reject(value);
      };
      const listening = () => {
        socket.off('error', error);
        resolve();
      };
      socket.once('error', error);
      socket.once('listening', listening);
      socket.bind(port, address);
    });
  }

  attach(writer) {
    if (!writer || typeof writer.write !== 'function') {
      throw new TypeError('VP9 RTP bridge requires a writable IVF pipe');
    }
    this.#writer = writer;
    writer.on('drain', () => {
      this.#backpressured = false;
    });
  }

  async close() {
    if (this.#closed) {
      return;
    }
    this.#closed = true;
    const socket = this.#socket;
    this.#socket = null;
    if (socket) {
      await new Promise((resolve, reject) => {
        try {
          socket.close(resolve);
        } catch (error) {
          if (error?.code === 'ERR_SOCKET_DGRAM_NOT_RUNNING') {
            resolve();
          } else {
            reject(error);
          }
        }
      });
    }
    this.#frame = null;
    this.#writer = null;
  }

  #receive(packet) {
    const rtp = parseRtpPacket(packet);
    if (!rtp) {
      return;
    }
    this.packets++;
    let vp9;
    try {
      vp9 = parseVp9Payload(rtp.payload);
    } catch {
      this.dropped++;
      this.#frame = null;
      return;
    }
    this.parsed++;
    if (vp9.spatialLayer !== this.spatialLayer ||
        vp9.temporalLayer !== this.temporalLayer) {
      this.ignoredLayers++;
      return;
    }
    if (vp9.beginning) {
      this.#frame = {
        timestamp: rtp.timestamp,
        width: vp9.width,
        height: vp9.height,
        sequence: rtp.sequence,
        parts: []
      };
    }
    if (!this.#frame || this.#frame.timestamp !== rtp.timestamp) {
      return;
    }
    if (this.#frame.parts.length > 0 &&
        ((this.#frame.sequence + 1) & 0xffff) !== rtp.sequence) {
      this.dropped++;
      this.#frame = null;
      return;
    }
    this.#frame.sequence = rtp.sequence;
    this.#frame.parts.push(vp9.payload);
    if (!vp9.end) {
      return;
    }

    const complete = this.#frame;
    this.#frame = null;
    if (!this.#writer || this.#writer.destroyed || this.#backpressured) {
      this.dropped++;
      return;
    }
    const payload = Buffer.concat(complete.parts);
    if (!this.#headerWritten) {
      this.#headerWritten = true;
      const header = ivfHeader(complete.width, complete.height);
      this.#backpressured = !this.#writer.write(header);
    }
    const timestamp = this.frames;
    if (!this.#backpressured) {
      const frame = ivfFrame(payload, timestamp);
      this.#backpressured = !this.#writer.write(frame);
      this.frames++;
    } else {
      this.dropped++;
    }
  }
}
