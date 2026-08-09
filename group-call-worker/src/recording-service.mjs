// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

import { spawn, spawnSync } from 'node:child_process';
import { createReadStream } from 'node:fs';
import {
  access,
  mkdir,
  readFile,
  rename,
  rm,
  stat,
  writeFile
} from 'node:fs/promises';
import { join } from 'node:path';

import { buildRtpTapSdp } from './broadcast-service.mjs';
import { Vp9RtpIvfBridge } from './vp9-rtp-ivf.mjs';

export class RecordingError extends Error {
  constructor(code, message) {
    super(message);
    this.name = 'RecordingError';
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

function validateRequest(callId, request) {
  safeCallId(callId);
  if (!request || typeof request !== 'object' || Array.isArray(request)) {
    throw new TypeError('recording request must be an object');
  }
  positiveInteger(request.generation, 'generation');
  positiveInteger(request.startedDate, 'startedDate');
  if (!Number.isSafeInteger(request.initiatingUserId) ||
      request.initiatingUserId <= 0) {
    throw new TypeError('initiatingUserId must be a positive safe integer');
  }
  if (typeof request.title !== 'string' || request.title.length > 1024) {
    throw new TypeError('recording title has invalid length');
  }
  if (typeof request.video !== 'boolean' ||
      typeof request.portrait !== 'boolean') {
    throw new TypeError('recording video and portrait must be booleans');
  }
  if (!request.video && request.portrait) {
    throw new TypeError('audio-only recording cannot be portrait');
  }
  return {
    callId,
    generation: request.generation,
    startedDate: request.startedDate,
    initiatingUserId: request.initiatingUserId,
    title: request.title,
    video: request.video,
    portrait: request.portrait
  };
}

function filterGraph({ audioCount, video, portrait, vp9Pipe }) {
  const sdpInput = vp9Pipe ? 1 : 0;
  const chains = [];
  const audioInputs = [];
  for (let index = 0; index < audioCount; index++) {
    const label = `a${index}`;
    chains.push(`[${sdpInput}:a:${index}]aresample=48000[${label}]`);
    audioInputs.push(`[${label}]`);
  }
  if (audioInputs.length === 1) {
    chains.push(`${audioInputs[0]}anull[aout]`);
  } else {
    chains.push(`${audioInputs.join('')}amix=inputs=${audioInputs.length}:` +
      'duration=longest:dropout_transition=2[aout]');
  }
  if (video) {
    const width = portrait ? 720 : 1280;
    const height = portrait ? 1280 : 720;
    const videoInput = vp9Pipe ? 0 : sdpInput;
    chains.push(`[${videoInput}:v:0]scale=${width}:${height}:` +
      `force_original_aspect_ratio=decrease,pad=${width}:${height}:` +
      '(ow-iw)/2:(oh-ih)/2:black,setsar=1[vout]');
  }
  return chains.join(';');
}

export function buildRecordingFfmpegArguments({
  sdpPath,
  outputPath,
  audioCount,
  video,
  portrait,
  vp9Pipe = false
}) {
  positiveInteger(audioCount, 'audioCount');
  if (typeof sdpPath !== 'string' || sdpPath.length === 0 ||
      typeof outputPath !== 'string' || outputPath.length === 0) {
    throw new TypeError('recording SDP and output paths are required');
  }
  const args = ['-hide_banner', '-loglevel', 'warning', '-y'];
  if (vp9Pipe) {
    args.push('-fflags', '+genpts+nobuffer', '-probesize', '32',
        '-analyzeduration', '0', '-f', 'ivf', '-i', 'pipe:3');
  }
  args.push('-protocol_whitelist', 'file,udp,rtp',
      '-fflags', '+genpts+nobuffer', '-f', 'sdp', '-i', sdpPath,
      '-filter_complex', filterGraph({
        audioCount, video, portrait, vp9Pipe
      }), '-map', '[aout]', '-c:a', 'aac', '-b:a', '96k', '-ar', '48000',
      '-ac', '2');
  if (video) {
    args.push('-map', '[vout]', '-c:v', 'libx264', '-pix_fmt', 'yuv420p',
        '-preset', 'veryfast', '-tune', 'zerolatency', '-g', '50',
        '-keyint_min', '25', '-sc_threshold', '0');
  }
  args.push('-movflags', '+faststart', '-f', 'mp4', outputPath);
  return args;
}

function probeRecording(ffprobePath, path) {
  const result = spawnSync(ffprobePath, [
    '-v', 'error', '-of', 'json', '-show_entries',
    'format=duration:stream=codec_type,width,height', path
  ], { encoding: 'utf8', timeout: 5000 });
  if (result.status !== 0) {
    throw new RecordingError('INVALID_OUTPUT',
        `ffprobe rejected finalized recording: ${result.stderr?.trim() ?? ''}`);
  }
  let value;
  try {
    value = JSON.parse(result.stdout);
  } catch {
    throw new RecordingError('INVALID_OUTPUT',
        'ffprobe returned invalid recording metadata');
  }
  const duration = Number(value.format?.duration ?? 0);
  const video = value.streams?.find((stream) => stream.codec_type === 'video');
  if (!Number.isFinite(duration) || duration <= 0) {
    throw new RecordingError('INVALID_OUTPUT',
        'finalized recording has no positive duration');
  }
  return {
    duration,
    width: Number(video?.width ?? 0),
    height: Number(video?.height ?? 0)
  };
}

async function exists(path) {
  try {
    await access(path);
    return true;
  } catch {
    return false;
  }
}

export class GroupCallRecordingService {
  #root;
  #mediaPlane;
  #broadcastService;
  #tapAddress;
  #tapMinPort;
  #tapMaxPort;
  #maxRecordings;
  #maxBytes;
  #maxDurationMs;
  #sourceWaitMs;
  #stopTimeoutMs;
  #ffmpegPath;
  #ffprobePath;
  #recordings = new Map();
  #stopping = false;

  constructor({
    root,
    mediaPlane,
    broadcastService = null,
    tapAddress = '127.0.0.1',
    tapMinPort = 50200,
    tapMaxPort = 50399,
    maxRecordings = 16,
    maxBytes = 2_000_000_000,
    maxDurationMs = 4 * 60 * 60 * 1000,
    sourceWaitMs = 30_000,
    stopTimeoutMs = 10_000,
    ffmpegPath = 'ffmpeg',
    ffprobePath = 'ffprobe'
  }) {
    if (typeof root !== 'string' || root.length === 0 ||
        !mediaPlane || typeof mediaPlane.getRtpTapSources !== 'function') {
      throw new TypeError('recording root and media plane are required');
    }
    positiveInteger(tapMinPort, 'tapMinPort');
    positiveInteger(tapMaxPort, 'tapMaxPort');
    positiveInteger(maxRecordings, 'maxRecordings');
    positiveInteger(maxBytes, 'maxBytes');
    positiveInteger(maxDurationMs, 'maxDurationMs');
    positiveInteger(sourceWaitMs, 'sourceWaitMs');
    positiveInteger(stopTimeoutMs, 'stopTimeoutMs');
    if (tapMinPort > tapMaxPort || tapMaxPort > 65535) {
      throw new TypeError('invalid recording RTP tap port range');
    }
    this.#root = root;
    this.#mediaPlane = mediaPlane;
    this.#broadcastService = broadcastService;
    this.#tapAddress = tapAddress;
    this.#tapMinPort = tapMinPort;
    this.#tapMaxPort = tapMaxPort;
    this.#maxRecordings = maxRecordings;
    this.#maxBytes = maxBytes;
    this.#maxDurationMs = maxDurationMs;
    this.#sourceWaitMs = sourceWaitMs;
    this.#stopTimeoutMs = stopTimeoutMs;
    this.#ffmpegPath = ffmpegPath;
    this.#ffprobePath = ffprobePath;
  }

  get ffmpegVersion() {
    const result = spawnSync(this.#ffmpegPath, ['-version'], {
      encoding: 'utf8', timeout: 2000
    });
    return result.status === 0
      ? result.stdout.split(/\r?\n/, 1)[0].trim()
      : null;
  }

  async startRecording(callId, rawRequest) {
    const request = validateRequest(callId, rawRequest);
    const existing = this.#recordings.get(callId);
    if (existing) {
      if (existing.request.generation !== request.generation) {
        throw new RecordingError('GENERATION_CONFLICT',
            'another recording generation is active for this call');
      }
      return;
    }
    if (this.#recordings.size >= this.#maxRecordings) {
      throw new RecordingError('CAPACITY',
          'recording capacity is exhausted');
    }

    const directory = join(this.#root, `${callId}-${request.generation}`);
    const finalPath = join(directory, request.video ? 'final.mp4' : 'final.m4a');
    const metadataPath = join(directory, 'metadata.json');
    await mkdir(directory, { recursive: true });
    if (await exists(metadataPath)) {
      const stored = JSON.parse(await readFile(metadataPath, 'utf8'));
      if (JSON.stringify(stored) !== JSON.stringify(request)) {
        throw new RecordingError('GENERATION_CONFLICT',
            'persisted recording metadata does not match the request');
      }
    } else {
      await writeFile(metadataPath, JSON.stringify(request), { encoding: 'utf8' });
    }

    const state = {
      request,
      directory,
      metadataPath,
      outputPath: join(directory, 'active.part'),
      audioPath: join(directory, 'audio.part.m4a'),
      finalPath,
      subscriberId: `recording-${callId}-${request.generation}`,
      tapPorts: [],
      process: null,
      processDone: null,
      sourcePoller: null,
      sourceSyncing: false,
      sourceDeadline: Date.now() + this.#sourceWaitMs,
      durationTimer: null,
      keyFramePoller: null,
      vp9Bridge: null,
      finalized: false,
      finalizing: null,
      metadata: null,
      lastError: null,
      stderr: '',
      startedAtMs: Date.now(),
      useBroadcastVideo: request.video && this.#broadcastService !== null,
      captureStopped: false
    };
    this.#recordings.set(callId, state);
    if (await exists(finalPath)) {
      await this.#loadFinalized(state);
      return;
    }
    await rm(state.outputPath, { force: true });
    this.#startSourcePolling(state);
  }

  async finalizeRecording(callId, generation) {
    const state = this.#require(callId, generation);
    if (!state.finalizing) {
      state.finalizing = this.#finalizeInternal(state).finally(() => {
        state.finalizing = null;
      });
    }
    await state.finalizing;
    const file = await stat(state.finalPath);
    return {
      path: state.finalPath,
      length: file.size,
      fileName: state.request.video
        ? `group-call-${callId}.mp4`
        : `group-call-${callId}.m4a`,
      mimeType: state.request.video ? 'video/mp4' : 'audio/mp4',
      duration: state.metadata.duration,
      width: state.metadata.width,
      height: state.metadata.height,
      stream: () => createReadStream(state.finalPath)
    };
  }

  async acknowledgeRecording(callId, generation) {
    const state = this.#require(callId, generation);
    if (!state.finalized) {
      throw new RecordingError('NOT_READY', 'recording is not finalized');
    }
    this.#recordings.delete(callId);
    await rm(state.directory, { recursive: true, force: true });
  }

  async cancelRecording(callId, generation) {
    const state = this.#recordings.get(callId);
    if (!state || state.request.generation !== generation) {
      return false;
    }
    this.#recordings.delete(callId);
    await this.#stopProcess(state, false);
    await rm(state.directory, { recursive: true, force: true });
    return true;
  }

  async health() {
    const states = [...this.#recordings.values()];
    let bytes = 0;
    for (const state of states) {
      try {
        bytes += (await stat(state.finalized
          ? state.finalPath
          : state.outputPath)).size;
      } catch {
      }
    }
    return {
      healthy: !this.#stopping && this.ffmpegVersion !== null &&
        states.every((state) => state.lastError === null),
      activeRecordings: states.filter((state) => !state.finalized).length,
      finalizedRecordings: states.filter((state) => state.finalized).length,
      bytes,
      ffmpegVersion: this.ffmpegVersion
    };
  }

  async close() {
    this.#stopping = true;
    for (const state of this.#recordings.values()) {
      if (!state.finalized && state.process) {
        try {
          await this.#finalizeInternal(state);
        } catch (error) {
          state.lastError = error?.message ?? String(error);
        }
      } else {
        await this.#stopProcess(state, false);
      }
    }
  }

  #startSourcePolling(state) {
    const sync = () => this.#startFromSources(state).catch((error) => {
      state.lastError = error?.message ?? String(error);
    });
    void sync();
    state.sourcePoller = setInterval(sync, 250);
    state.sourcePoller.unref();
  }

  async #startFromSources(state) {
    if (state.sourceSyncing || state.process || state.finalized || state.finalizing ||
        !this.#recordings.has(state.request.callId)) {
      return;
    }
    state.sourceSyncing = true;
    try {
    const sources = this.#mediaPlane.getRtpTapSources(state.request.callId);
    const audio = sources.filter((source) => source.kind === 'audio').slice(0, 32);
    const video = state.request.video
      ? sources.filter((source) => source.kind === 'video')
          .sort((left, right) => Number(right.presentation) -
            Number(left.presentation))[0]
      : null;
    if (audio.length === 0 || (state.request.video && !video)) {
      if (Date.now() >= state.sourceDeadline) {
        clearInterval(state.sourcePoller);
        state.sourcePoller = null;
        state.lastError = 'recording source wait timed out';
      }
      return;
    }
    clearInterval(state.sourcePoller);
    state.sourcePoller = null;
    const selected = video && !state.useBroadcastVideo
      ? [...audio, video]
      : audio;
    state.tapPorts = this.#allocateTapPorts(selected.length);
    const targets = selected.map((source, index) => ({
      participantId: source.participantId,
      presentation: source.presentation,
      kind: source.kind,
      ip: this.#tapAddress,
      port: state.tapPorts[index],
      paused: true
    }));
    const vp9 = video?.codec?.toLowerCase() === 'video/vp9';
    try {
      if (vp9) {
        const videoIndex = selected.indexOf(video);
        state.vp9Bridge = new Vp9RtpIvfBridge();
        await state.vp9Bridge.bind(this.#tapAddress, state.tapPorts[videoIndex]);
      }
      let descriptors = await this.#mediaPlane.acquireRtpTap(
          state.request.callId, state.subscriberId, targets);
      descriptors = descriptors.map((descriptor, index) => ({
        ...descriptor,
        targetPort: state.tapPorts[index]
      }));
      const sdpDescriptors = vp9
        ? descriptors.filter((descriptor) => descriptor.kind !== 'video')
        : descriptors;
      const sdpPath = join(state.directory, 'tap.sdp');
      await writeFile(sdpPath, buildRtpTapSdp(sdpDescriptors), {
        encoding: 'utf8'
      });
      await this.#spawn(state, sdpPath, audio.length, vp9);
    } catch (error) {
      await state.vp9Bridge?.close();
      state.vp9Bridge = null;
      this.#mediaPlane.releaseRtpTap(state.request.callId, state.subscriberId);
      state.tapPorts = [];
      throw error;
    }
    } finally {
      state.sourceSyncing = false;
    }
  }

  async #spawn(state, sdpPath, audioCount, vp9Pipe) {
    const args = buildRecordingFfmpegArguments({
      sdpPath,
      outputPath: state.outputPath,
      audioCount,
      video: state.request.video && !state.useBroadcastVideo,
      portrait: state.request.portrait,
      vp9Pipe
    });
    const child = spawn(this.#ffmpegPath, args, {
      stdio: vp9Pipe
        ? ['pipe', 'ignore', 'pipe', 'pipe']
        : ['pipe', 'ignore', 'pipe']
    });
    if (vp9Pipe) {
      state.vp9Bridge.attach(child.stdio[3]);
    }
    state.process = child;
    state.stderr = '';
    let resolveDone;
    state.processDone = new Promise((resolve) => {
      resolveDone = resolve;
    });
    child.stderr.on('data', (chunk) => {
      state.stderr = (state.stderr + chunk.toString('utf8')).slice(-8192);
    });
    child.once('error', (error) => {
      state.lastError = error.message;
    });
    child.once('exit', (status) => {
      if (state.process === child) {
        state.process = null;
      }
      if (status !== 0 && !state.finalizing) {
        state.lastError = state.stderr || `FFmpeg exited with status ${status}`;
      }
      resolveDone();
    });
    await new Promise((resolve) => setTimeout(resolve, 250));
    if (state.process !== child) {
      throw new RecordingError('UNAVAILABLE',
          state.stderr || 'recording FFmpeg failed to start');
    }
    await this.#mediaPlane.resumeRtpTap(state.request.callId, state.subscriberId);
    if (state.request.video) {
      const requestKeyFrame = () => this.#mediaPlane.requestRtpTapKeyFrames(
          state.request.callId, state.subscriberId).catch((error) => {
        state.lastError = error?.message ?? String(error);
      });
      state.keyFramePoller = setInterval(requestKeyFrame, 1000);
      state.keyFramePoller.unref();
      requestKeyFrame();
    }
    state.durationTimer = setTimeout(() => {
      this.#finalizeInternal(state).catch((error) => {
        state.lastError = error?.message ?? String(error);
      });
    }, this.#maxDurationMs);
    state.durationTimer.unref();
  }

  async #finalizeInternal(state) {
    if (state.finalized) {
      return;
    }
    if (!state.captureStopped) {
      if (!state.process) {
        if (state.lastError) {
          throw new RecordingError('UNAVAILABLE', state.lastError);
        }
        throw new RecordingError('NOT_READY',
            'recording has not received media yet');
      }
      await this.#stopProcess(state, true);
      state.captureStopped = true;
      if (state.useBroadcastVideo) {
        await rename(state.outputPath, state.audioPath);
      }
    }
    if (state.useBroadcastVideo) {
      await this.#muxSharedVideo(state);
    }
    const output = await stat(state.outputPath);
    if (output.size <= 0 || output.size > this.#maxBytes) {
      const diagnostic = state.stderr.trim();
      throw new RecordingError('LIMIT',
          'finalized recording violates the configured size bound' +
          (diagnostic.length > 0 ? `: ${diagnostic}` : ''));
    }
    state.metadata = probeRecording(this.#ffprobePath, state.outputPath);
    await rename(state.outputPath, state.finalPath);
    state.finalized = true;
    state.lastError = null;
  }

  async #muxSharedVideo(state) {
    const videoDirectory = join(state.directory, 'shared-video');
    await rm(videoDirectory, { recursive: true, force: true });
    const maxSegments = Math.ceil(this.#maxDurationMs / 1000) + 2;
    let names;
    try {
      names = await this.#broadcastService.copyRecordingVideoSegments(
          state.request.callId, state.startedAtMs, videoDirectory, {
            waitMs: this.#sourceWaitMs, maxSegments
          });
    } catch (error) {
      throw new RecordingError('NOT_READY',
          `shared recording video is unavailable: ${error?.message ?? error}`);
    }
    const concatPath = join(videoDirectory, 'segments.concat');
    await writeFile(concatPath,
        names.map((name) => `file '${name}'`).join('\n') + '\n', {
          encoding: 'utf8', mode: 0o600
        });
    await rm(state.outputPath, { force: true });
    const width = state.request.portrait ? 720 : 1280;
    const height = state.request.portrait ? 1280 : 720;
    const graph = `[0:v:0]scale=${width}:${height}:` +
      `force_original_aspect_ratio=decrease,pad=${width}:${height}:` +
      '(ow-iw)/2:(oh-ih)/2:black,setsar=1[vout]';
    const args = [
      '-hide_banner', '-loglevel', 'warning', '-y',
      '-f', 'concat', '-safe', '1', '-i', concatPath,
      '-i', state.audioPath,
      '-filter_complex', graph,
      '-map', '[vout]', '-c:v', 'libx264', '-pix_fmt', 'yuv420p',
      '-preset', 'veryfast', '-tune', 'zerolatency', '-g', '50',
      '-keyint_min', '25', '-sc_threshold', '0',
      '-map', '1:a:0', '-c:a', 'copy', '-shortest',
      '-movflags', '+faststart', '-f', 'mp4', state.outputPath
    ];
    const child = spawn(this.#ffmpegPath, args, {
      stdio: ['ignore', 'ignore', 'pipe']
    });
    let stderr = '';
    child.stderr.on('data', (chunk) => {
      stderr = (stderr + chunk.toString('utf8')).slice(-8192);
    });
    const status = await new Promise((resolve, reject) => {
      child.once('error', reject);
      child.once('exit', resolve);
      const timer = setTimeout(() => {
        child.kill('SIGKILL');
        reject(new RecordingError('UNAVAILABLE',
            'shared video mux timed out'));
      }, this.#stopTimeoutMs);
      timer.unref();
      child.once('exit', () => clearTimeout(timer));
    });
    if (status !== 0) {
      throw new RecordingError('UNAVAILABLE',
          stderr || `shared video mux exited with status ${status}`);
    }
    state.stderr = stderr;
  }

  async #loadFinalized(state) {
    const file = await stat(state.finalPath);
    if (file.size <= 0 || file.size > this.#maxBytes) {
      throw new RecordingError('LIMIT',
          'persisted recording violates the configured size bound');
    }
    state.metadata = probeRecording(this.#ffprobePath, state.finalPath);
    state.finalized = true;
  }

  async #stopProcess(state, graceful) {
    if (state.sourcePoller) {
      clearInterval(state.sourcePoller);
      state.sourcePoller = null;
    }
    if (state.durationTimer) {
      clearTimeout(state.durationTimer);
      state.durationTimer = null;
    }
    if (state.keyFramePoller) {
      clearInterval(state.keyFramePoller);
      state.keyFramePoller = null;
    }
    if (state.process) {
      const child = state.process;
      const done = state.processDone;
      if (graceful && child.stdin?.writable) {
        child.stdin.end('q\n');
      } else {
        child.kill('SIGTERM');
      }
      let exited = false;
      if (graceful) {
        exited = await Promise.race([
          done.then(() => true),
          new Promise((resolve) => setTimeout(() => resolve(false), 2000))
        ]);
        if (!exited) {
          child.kill('SIGTERM');
        }
      }
      let killTimer;
      if (!exited) {
        await Promise.race([
          done,
          new Promise((resolve) => {
            killTimer = setTimeout(() => {
              child.kill('SIGKILL');
              resolve();
            }, this.#stopTimeoutMs);
            killTimer.unref();
          })
        ]);
      }
      clearTimeout(killTimer);
      state.process = null;
      state.processDone = null;
    }
    await state.vp9Bridge?.close();
    state.vp9Bridge = null;
    this.#mediaPlane.releaseRtpTap?.(state.request.callId, state.subscriberId);
    state.tapPorts = [];
  }

  #allocateTapPorts(count) {
    const used = new Set([...this.#recordings.values()]
        .flatMap((state) => state.tapPorts.flatMap((port) => [port, port + 1])));
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
      throw new RecordingError('CAPACITY',
          'recording RTP tap listener capacity is exhausted');
    }
    return result;
  }

  #require(callId, generation) {
    safeCallId(callId);
    positiveInteger(generation, 'generation');
    const state = this.#recordings.get(callId);
    if (!state) {
      throw new RecordingError('NOT_FOUND', 'recording is missing');
    }
    if (state.request.generation !== generation) {
      throw new RecordingError('GENERATION_CONFLICT',
          'recording generation does not match');
    }
    return state;
  }
}
