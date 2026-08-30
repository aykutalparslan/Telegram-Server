// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

import { mkdir, readFile, rename, rm, writeFile } from 'node:fs/promises';
import { join } from 'node:path';

function segmentKey(timestamp, channel, quality) {
  return `${timestamp}:${channel}:${quality}`;
}

function validateTimestamp(timestamp) {
  if (!Number.isSafeInteger(timestamp) || timestamp < 0 ||
      timestamp % 1000 !== 0) {
    throw new TypeError('segment timestamp must be a non-negative second boundary');
  }
}

export class SegmentRing {
  #root;
  #retentionMs;
  #maxSegments;
  #maxBytes;
  #maxSegmentBytes;
  #segments = new Map();
  #bytes = 0;
  #latestTimestamp = 0;

  constructor({
    root,
    retentionMs = 5 * 60 * 1000,
    maxSegments = 4096,
    maxBytes = 512 * 1024 * 1024,
    maxSegmentBytes = 1024 * 1024
  }) {
    if (typeof root !== 'string' || root.length === 0) {
      throw new TypeError('segment ring root must not be empty');
    }
    if (!Number.isInteger(retentionMs) || retentionMs < 3000 ||
        !Number.isInteger(maxSegments) || maxSegments < 3 ||
        !Number.isInteger(maxBytes) || maxBytes < maxSegmentBytes ||
        !Number.isInteger(maxSegmentBytes) || maxSegmentBytes < 4096) {
      throw new TypeError('invalid segment ring bounds');
    }
    this.#root = root;
    this.#retentionMs = retentionMs;
    this.#maxSegments = maxSegments;
    this.#maxBytes = maxBytes;
    this.#maxSegmentBytes = maxSegmentBytes;
  }

  get latestTimestamp() {
    return this.#latestTimestamp;
  }

  get count() {
    return this.#segments.size;
  }

  get bytes() {
    return this.#bytes;
  }

  latestTimestampForChannel(channel) {
    if (!Number.isInteger(channel) || channel < 0) {
      throw new TypeError('segment channel must be a non-negative integer');
    }
    let latestTimestamp = 0;
    for (const segment of this.#segments.values()) {
      if (segment.channel === channel) {
        latestTimestamp = Math.max(latestTimestamp, segment.timestamp);
      }
    }
    return latestTimestamp;
  }

  async initialize() {
    await mkdir(this.#root, { recursive: true });
  }

  async put({ timestamp, channel, quality = 0, bytes }) {
    validateTimestamp(timestamp);
    if (!Number.isInteger(channel) || channel < 0 ||
        !Number.isInteger(quality) || quality < 0 || quality > 2) {
      throw new TypeError('invalid segment channel or quality');
    }
    if (!Buffer.isBuffer(bytes) || bytes.length === 0 ||
        bytes.length > this.#maxSegmentBytes) {
      throw new RangeError('segment payload violates the configured byte bound');
    }

    await this.initialize();
    const key = segmentKey(timestamp, channel, quality);
    const finalPath = join(this.#root, `${timestamp}-${channel}-${quality}.segment`);
    const temporaryPath = `${finalPath}.tmp-${process.pid}-${Date.now()}`;
    await writeFile(temporaryPath, bytes, { flag: 'wx', mode: 0o600 });
    await rename(temporaryPath, finalPath);

    const previous = this.#segments.get(key);
    if (previous) {
      this.#bytes -= previous.size;
      if (previous.path !== finalPath) {
        await rm(previous.path, { force: true });
      }
    }
    this.#segments.set(key, {
      key,
      timestamp,
      channel,
      quality,
      size: bytes.length,
      path: finalPath
    });
    this.#bytes += bytes.length;
    this.#latestTimestamp = Math.max(this.#latestTimestamp, timestamp);
    await this.#evict();
  }

  async read({ timestamp, channel, quality = 0 }) {
    validateTimestamp(timestamp);
    if (!Number.isInteger(channel) || channel < 0) {
      throw new TypeError('segment channel must be a non-negative integer');
    }
    const exact = this.#segments.get(segmentKey(timestamp, channel, quality));
    if (exact) {
      return await readFile(exact.path);
    }

    // Single-rendition fallback: quality is a client preference and all three
    // values resolve to the only available file while the ladder is disabled.
    const fallback = [...this.#segments.values()].find((segment) =>
      segment.timestamp === timestamp && segment.channel === channel);
    return fallback ? await readFile(fallback.path) : null;
  }

  channels() {
    const latestByChannel = new Map();
    let audioLiveEdge = 0;
    for (const segment of this.#segments.values()) {
      if (segment.channel === 0) {
        audioLiveEdge = Math.max(audioLiveEdge, segment.timestamp);
        continue;
      }
      latestByChannel.set(segment.channel,
          Math.max(latestByChannel.get(segment.channel) ?? 0,
              segment.timestamp));
    }
    if (audioLiveEdge === 0) {
      return [];
    }
    return [...latestByChannel]
        .sort(([left], [right]) => left - right)
        .map(([channel, videoLiveEdge]) => ({
          channel,
          scale: 0,
          // A client requests audio and its chosen video channel at the same
          // timestamp. Advertise only the common completed edge; FFmpeg's two
          // muxers can finish their current files a few hundred milliseconds
          // apart.
          lastTimestampMs: Math.min(videoLiveEdge, audioLiveEdge)
        }));
  }

  async clear() {
    this.#segments.clear();
    this.#bytes = 0;
    this.#latestTimestamp = 0;
    await rm(this.#root, { recursive: true, force: true });
  }

  async #evict() {
    const minimumTimestamp = Math.max(0,
        this.#latestTimestamp - this.#retentionMs);
    const ordered = [...this.#segments.values()].sort((left, right) =>
      left.timestamp - right.timestamp || left.channel - right.channel ||
      left.quality - right.quality);
    for (const segment of ordered) {
      if (segment.timestamp >= minimumTimestamp &&
          this.#segments.size <= this.#maxSegments &&
          this.#bytes <= this.#maxBytes) {
        continue;
      }
      this.#segments.delete(segment.key);
      this.#bytes -= segment.size;
      await rm(segment.path, { force: true });
    }
  }
}
