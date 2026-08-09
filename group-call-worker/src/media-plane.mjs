// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

import * as mediasoup from 'mediasoup';

import { selectPreferredLayers } from './layer-selection.mjs';

// Video codec probe (see #watchProducerCodec): how often to re-sample, how many
// samples to take, and how many unaccounted received bytes in one interval mean
// the client is really sending video the producer is dropping. The window is
// generous because it only ever runs until the first accepted RTP.
const CODEC_PROBE_INTERVAL_MS = 700;
const CODEC_PROBE_ATTEMPTS = 12;
const CODEC_PROBE_MIN_UNACCOUNTED_BYTES = 8000;

function delay(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

function sumStat(stats, field) {
  let total = 0;
  for (const entry of stats) {
    const value = entry?.[field];
    if (Number.isFinite(value)) {
      total += value;
    }
  }
  return total;
}

const OPUS_CAPABILITY = {
  kind: 'audio',
  mimeType: 'audio/opus',
  preferredPayloadType: 111,
  clockRate: 48000,
  channels: 2,
  parameters: {
    minptime: 10,
    useinbandfec: 1
  },
  rtcpFeedback: [
    { type: 'transport-cc', parameter: '' }
  ]
};

// The feedback set the pinned tgcalls client expects on a video payload type.
const VIDEO_FEEDBACK = [
  { type: 'nack', parameter: '' },
  { type: 'nack', parameter: 'pli' },
  { type: 'ccm', parameter: 'fir' },
  { type: 'goog-remb', parameter: '' },
  { type: 'transport-cc', parameter: '' }
];

// Offered in preference order. Payload types are deliberately NOT pinned here:
// mediasoup derives an RTX codec per media codec itself, rejects an explicit
// video/rtx entry, and allocates the dynamic range on its own. The real numbers
// are read back from the router's capabilities after start().
const VIDEO_CAPABILITIES = [
  {
    kind: 'video',
    mimeType: 'video/VP8',
    clockRate: 90000,
    parameters: {},
    rtcpFeedback: VIDEO_FEEDBACK
  },
  {
    kind: 'video',
    mimeType: 'video/VP9',
    clockRate: 90000,
    parameters: { 'profile-id': 0 },
    rtcpFeedback: VIDEO_FEEDBACK
  },
  {
    kind: 'video',
    mimeType: 'video/H264',
    clockRate: 90000,
    parameters: {
      'packetization-mode': 1,
      'profile-level-id': '42e01f',
      'level-asymmetry-allowed': 1
    },
    rtcpFeedback: VIDEO_FEEDBACK
  }
];

// Resolve each offered video codec to the payload types the router actually
// assigned, plus its RTX companion.
function resolveVideoCodecs(routerCapabilities) {
  const resolved = [];
  for (const capability of VIDEO_CAPABILITIES) {
    const media = routerCapabilities.codecs.find(
        (codec) => codec.mimeType.toLowerCase() ===
          capability.mimeType.toLowerCase());
    if (!media) {
      throw new Error(`router did not accept ${capability.mimeType}`);
    }
    const rtx = routerCapabilities.codecs.find(
        (codec) => codec.mimeType.toLowerCase() === 'video/rtx' &&
          codec.parameters?.apt === media.preferredPayloadType);
    resolved.push({
      name: capability.mimeType.slice('video/'.length),
      mimeType: media.mimeType,
      payloadType: media.preferredPayloadType,
      rtxPayloadType: rtx?.preferredPayloadType ?? null,
      clockRate: media.clockRate,
      parameters: media.parameters ?? {},
      rtcpFeedback: media.rtcpFeedback ?? VIDEO_FEEDBACK
    });
  }
  return resolved;
}

const VIDEO_HEADER_EXTENSIONS = [
  {
    uri: 'http://www.webrtc.org/experiments/rtp-hdrext/abs-send-time',
    id: 2,
    encrypt: false,
    parameters: {}
  },
  {
    uri: 'http://www.ietf.org/id/draft-holmer-rmcat-transport-wide-cc-extensions-01',
    id: 3,
    encrypt: false,
    parameters: {}
  },
  {
    uri: 'urn:3gpp:video-orientation',
    id: 13,
    encrypt: false,
    parameters: {}
  }
];

const AUDIO_HEADER_EXTENSIONS = [
  {
    uri: 'urn:ietf:params:rtp-hdrext:ssrc-audio-level',
    id: 1,
    encrypt: false,
    parameters: {}
  },
  {
    uri: 'http://www.webrtc.org/experiments/rtp-hdrext/abs-send-time',
    id: 2,
    encrypt: false,
    parameters: {}
  },
  {
    uri: 'http://www.ietf.org/id/draft-holmer-rmcat-transport-wide-cc-extensions-01',
    id: 3,
    encrypt: false,
    parameters: {}
  }
];

function requireObject(value, name) {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    throw new TypeError(`${name} must be an object`);
  }
  return value;
}

function parseClientPayload(value) {
  const payload = requireObject(value, 'join payload');
  if (!Number.isInteger(payload.ssrc) || payload.ssrc === 0 ||
      payload.ssrc < -2147483648 || payload.ssrc > 2147483647) {
    throw new TypeError('join payload ssrc must be a nonzero signed int32');
  }
  if (typeof payload.ufrag !== 'string' || payload.ufrag.length === 0 ||
      typeof payload.pwd !== 'string' || payload.pwd.length === 0) {
    throw new TypeError('join payload requires ICE ufrag and pwd');
  }
  if (!Array.isArray(payload.fingerprints) ||
      payload.fingerprints.length === 0) {
    throw new TypeError('join payload requires a fingerprint');
  }

  const fingerprints = payload.fingerprints.map((fingerprint) => {
    requireObject(fingerprint, 'fingerprint');
    if (fingerprint.setup !== 'passive') {
      throw new TypeError('current tgcalls must offer DTLS setup passive');
    }
    if (typeof fingerprint.hash !== 'string' ||
        typeof fingerprint.fingerprint !== 'string') {
      throw new TypeError('invalid fingerprint');
    }
    return {
      algorithm: fingerprint.hash,
      value: fingerprint.fingerprint
    };
  });

  return {
    source: payload.ssrc >>> 0,
    fingerprints,
    video: parseVideoSourceGroups(payload),
    raw: payload
  };
}

// The pinned client advertises its video layers as explicit SSRCs: one SIM
// group listing the simulcast layers in quality order, plus one FID pair per
// layer binding it to its RTX stream. There is no RID, so mediasoup drives
// simulcast off these SSRCs directly.
function parseVideoSourceGroups(payload) {
  const groups = payload['ssrc-groups'];
  if (!Array.isArray(groups) || groups.length === 0) {
    return null;
  }

  const rtxByPrimary = new Map();
  let simulcast = null;
  for (const group of groups) {
    requireObject(group, 'ssrc group');
    if (!Array.isArray(group.sources) || group.sources.length === 0) {
      throw new TypeError('ssrc group requires sources');
    }
    const sources = group.sources.map((source) => {
      if (!Number.isInteger(source) || source === 0) {
        throw new TypeError('ssrc group source must be a nonzero integer');
      }
      return source >>> 0;
    });

    if (group.semantics === 'SIM') {
      if (simulcast) {
        throw new TypeError('ssrc groups declare more than one SIM group');
      }
      simulcast = sources;
    } else if (group.semantics === 'FID') {
      if (sources.length !== 2) {
        throw new TypeError('FID group must pair exactly two sources');
      }
      rtxByPrimary.set(sources[0], sources[1]);
    }
  }

  // Without a SIM group the client is sending a single video layer, which is
  // the primary of its only FID pair.
  const layers = simulcast ?? [...rtxByPrimary.keys()].slice(0, 1);
  if (layers.length === 0) {
    return null;
  }

  return { layers, rtxByPrimary, groups };
}

function videoProducerRtpParameters(video, participantId, codec, videoCodecs) {
  const media = videoCodecs.find(
      (entry) => entry.name.toLowerCase() === codec.toLowerCase());
  if (!media) {
    throw new TypeError(`unsupported video codec ${codec}`);
  }

  const codecs = [
    {
      mimeType: media.mimeType,
      payloadType: media.payloadType,
      clockRate: media.clockRate,
      parameters: media.parameters,
      rtcpFeedback: media.rtcpFeedback
    }
  ];
  if (media.rtxPayloadType !== null) {
    codecs.push({
      mimeType: 'video/rtx',
      payloadType: media.rtxPayloadType,
      clockRate: media.clockRate,
      parameters: { apt: media.payloadType },
      rtcpFeedback: []
    });
  }

  // mediasoup rejects SSRC-based simulcast for VP9 outright ("video/VP9 codec
  // not supported for simulcast"): VP9 expresses layers as SVC inside a single
  // stream, not as parallel SSRCs. The client still advertises a SIM group, so
  // forward only its first layer rather than failing the join.
  const simulcastCapable = media.mimeType.toLowerCase() !== 'video/vp9';
  const layers = simulcastCapable ? video.layers : video.layers.slice(0, 1);

  return {
    codecs,
    headerExtensions: VIDEO_HEADER_EXTENSIONS,
    encodings: layers.map((ssrc) => {
      const encoding = { ssrc };
      if (!simulcastCapable) {
        // Pinned tgcalls emits VP9 as one L3T3 SVC stream. Declaring the mode
        // lets mediasoup select layers for ordinary viewers and internal taps
        // instead of treating every spatial layer as an opaque L1 stream.
        encoding.scalabilityMode = 'L3T3';
      }
      const rtxSsrc = video.rtxByPrimary.get(ssrc);
      if (rtxSsrc) {
        encoding.rtx = { ssrc: rtxSsrc };
      }
      return encoding;
    }),
    rtcp: {
      cname: `ferrite-${participantId}`,
      reducedSize: true,
      mux: true
    }
  };
}

// The video half of the join answer, in the exact shape
// GroupJoinPayloadInternal.cpp::parseVideoInformation reads. Note that every
// payload-type parameter must be a STRING: the client drops non-string values.
function videoAnswer(endpoint, serverSource, videoCodecs) {
  const payloadTypes = [];
  for (const capability of videoCodecs) {
    const parameters = {};
    for (const [key, value] of Object.entries(capability.parameters)) {
      parameters[key] = String(value);
    }
    payloadTypes.push({
      id: capability.payloadType,
      name: capability.name,
      clockrate: capability.clockRate,
      channels: 1,
      parameters,
      'rtcp-fbs': capability.rtcpFeedback.map((feedback) =>
        feedback.parameter
          ? { type: feedback.type, subtype: feedback.parameter }
          : { type: feedback.type })
    });
    if (capability.rtxPayloadType !== null) {
      payloadTypes.push({
        id: capability.rtxPayloadType,
        name: 'rtx',
        clockrate: capability.clockRate,
        channels: 1,
        parameters: { apt: String(capability.payloadType) },
        'rtcp-fbs': []
      });
    }
  }

  return {
    endpoint,
    server_sources: [serverSource],
    'payload-types': payloadTypes,
    'rtp-hdrexts': VIDEO_HEADER_EXTENSIONS.map((extension) => ({
      id: extension.id,
      uri: extension.uri
    }))
  };
}

function telegramCandidate(candidate, index) {
  return {
    port: String(candidate.port),
    protocol: candidate.protocol,
    network: '1',
    generation: '0',
    id: String(index + 1),
    component: '1',
    foundation: candidate.foundation,
    priority: String(candidate.priority),
    ip: candidate.address ?? candidate.ip,
    type: candidate.type
  };
}

function telegramFingerprint(fingerprint) {
  return {
    hash: fingerprint.algorithm,
    fingerprint: fingerprint.value,
    setup: 'active'
  };
}

function producerRtpParameters(source, participantId) {
  return {
    codecs: [
      {
        mimeType: 'audio/opus',
        payloadType: 111,
        clockRate: 48000,
        channels: 2,
        parameters: {
          minptime: 10,
          useinbandfec: 1
        },
        rtcpFeedback: [
          { type: 'transport-cc', parameter: '' }
        ]
      }
    ],
    headerExtensions: AUDIO_HEADER_EXTENSIONS,
    encodings: [{ ssrc: source }],
    rtcp: {
      cname: `ferrite-${participantId}`,
      reducedSize: true,
      mux: true
    }
  };
}

function firstEncodingSource(consumer) {
  const source = consumer.rtpParameters?.encodings?.[0]?.ssrc;
  if (!Number.isInteger(source) || source === 0) {
    throw new Error('mediasoup consumer did not expose a nonzero source');
  }
  return source >>> 0;
}

export class GroupCallMediaPlane {
  #listenIp;
  #announcedAddress;
  #rtcMinPort;
  #rtcMaxPort;
  #maxRooms;
  #maxParticipantsPerRoom;
  #worker;
  #router;
  #videoCodecs = [];
  #rooms = new Map();
  #restarting;
  #deathListeners = new Set();
  #disconnectListeners = new Set();
  #sourcesChangedListeners = new Set();

  constructor({
    listenIp = '127.0.0.1',
    announcedAddress,
    rtcMinPort = 40000,
    rtcMaxPort = 40100,
    maxRooms = 100,
    maxParticipantsPerRoom = 1000
  } = {}) {
    if (!Number.isInteger(rtcMinPort) || !Number.isInteger(rtcMaxPort) ||
        rtcMinPort < 1 || rtcMaxPort > 65535 || rtcMinPort > rtcMaxPort) {
      throw new TypeError('invalid media RTC port range');
    }
    if (!Number.isInteger(maxRooms) || maxRooms < 1 ||
        !Number.isInteger(maxParticipantsPerRoom) ||
        maxParticipantsPerRoom < 1) {
      throw new TypeError('media limits must be positive integers');
    }
    this.#listenIp = listenIp;
    this.#announcedAddress = announcedAddress;
    this.#rtcMinPort = rtcMinPort;
    this.#rtcMaxPort = rtcMaxPort;
    this.#maxRooms = maxRooms;
    this.#maxParticipantsPerRoom = maxParticipantsPerRoom;
  }

  async start() {
    if (this.#worker) {
      return;
    }
    this.#worker = await mediasoup.createWorker({
      logLevel: 'warn',
      logTags: ['ice', 'dtls', 'rtp', 'rtcp', 'srtp']
    });
    this.#worker.on('died', (error) => {
      const correlations = [];
      for (const [callId, room] of this.#rooms) {
        for (const participant of room.participants.values()) {
          if (!participant.presentation) {
            correlations.push({
              callId,
              participantId: participant.ownerParticipantId
            });
          }
        }
      }
      const pid = this.#worker?.pid ?? 0;

      // The router, transports and producers all lived inside the worker
      // process, so they died with it. Drop the handles before notifying:
      // start() short-circuits on a non-null #worker, so leaving the dead one
      // in place would keep the service up with no media capability at all and
      // every later room operation would fail against a dead router.
      this.#rooms.clear();
      this.#worker = null;
      this.#router = null;
      this.#videoCodecs = [];

      for (const listener of this.#deathListeners) {
        listener({
          type: 'worker_died',
          pid,
          message: error?.message ?? 'mediasoup worker died',
          correlations
        });
      }

      // Nothing upstream recreates the worker, so respawn here. The rooms are
      // deliberately not rebuilt: the control plane evicts their participants
      // from the correlations above and clients rejoin into fresh rooms.
      this.#restarting = this.start().catch((restartError) => {
        for (const listener of this.#deathListeners) {
          listener({
            type: 'worker_died',
            pid: 0,
            message: 'mediasoup worker could not be restarted: ' +
              (restartError?.message ?? restartError),
            correlations: []
          });
        }
      });
    });
    this.#router = await this.#worker.createRouter({
      mediaCodecs: [OPUS_CAPABILITY, ...VIDEO_CAPABILITIES]
    });
    this.#videoCodecs = resolveVideoCodecs(this.#router.rtpCapabilities);
  }

  onWorkerDeath(listener) {
    this.#deathListeners.add(listener);
    return () => this.#deathListeners.delete(listener);
  }

  onDisconnect(listener) {
    this.#disconnectListeners.add(listener);
    return () => this.#disconnectListeners.delete(listener);
  }

  /// Fires when a call's per-viewer media mapping stopped matching what was
  /// last reported: today that is a video codec correction (see
  /// #watchProducerCodec), which re-creates consumers and therefore their
  /// rewritten SSRCs. The control plane must re-read getViewerMedia and
  /// re-publish the participant rows, or receivers keep listening on SSRCs that
  /// no longer carry anything.
  onSourcesChanged(listener) {
    this.#sourcesChangedListeners.add(listener);
    return () => this.#sourcesChangedListeners.delete(listener);
  }

  #publishSourcesChanged(callId, participantId, reason) {
    for (const listener of this.#sourcesChangedListeners) {
      listener({ callId, participantId, reason });
    }
  }

  get workerPid() {
    return this.#worker?.pid ?? 0;
  }

  async createRoom(callId) {
    if (!this.#router) {
      throw new Error('media plane is not started');
    }
    if (!this.#rooms.has(callId)) {
      if (this.#rooms.size >= this.#maxRooms) {
        throw new Error('media room limit reached');
      }
      this.#rooms.set(callId, { participants: new Map(), taps: new Map() });
    }
  }

  async join(callId, participantId, rawPayload, options = {}) {
    const room = this.#rooms.get(callId);
    if (!room) {
      throw new Error(`room ${callId} is missing`);
    }
    if (room.participants.has(participantId)) {
      throw new Error(`participant ${participantId} is already joined`);
    }
    const ordinaryParticipants = [...room.participants.values()]
        .filter((participant) => !participant.presentation).length;
    if (!options.presentation &&
        ordinaryParticipants >= this.#maxParticipantsPerRoom) {
      throw new Error('media participant limit reached');
    }

    const payload = parseClientPayload(rawPayload);
    for (const participant of room.participants.values()) {
      if (participant.source === payload.source) {
        throw new Error('GROUPCALL_SSRC_DUPLICATE_MUCH');
      }
    }

    const listenInfo = {
      protocol: 'udp',
      ip: this.#listenIp,
      portRange: { min: this.#rtcMinPort, max: this.#rtcMaxPort }
    };
    if (this.#announcedAddress) {
      listenInfo.announcedAddress = this.#announcedAddress;
    }
    const transport = await this.#router.createWebRtcTransport({
      listenInfos: [listenInfo],
      enableUdp: true,
      enableTcp: false,
      enableSctp: true,
      numSctpStreams: { OS: 1024, MIS: 1024 },
      maxSctpMessageSize: 262144,
      initialAvailableOutgoingBitrate: 1000000
    });

    try {
      await transport.connect({
        dtlsParameters: {
          role: 'server',
          fingerprints: payload.fingerprints
        }
      });

      const producer = await transport.produce({
        kind: 'audio',
        rtpParameters: producerRtpParameters(payload.source, participantId),
        paused: false,
        appData: { callId, participantId }
      });

      const videoCodec = options.videoCodec ?? 'VP8';
      const ownerParticipantId =
          options.ownerParticipantId ?? participantId;
      const endpoint = options.presentation
        ? `${ownerParticipantId}-screen`
        : `${ownerParticipantId}-cam`;
      let videoProducer = null;
      if (payload.video) {
        videoProducer = await transport.produce({
          kind: 'video',
          rtpParameters: videoProducerRtpParameters(
              payload.video, participantId, videoCodec, this.#videoCodecs),
          paused: false,
          appData: { callId, participantId, endpoint }
        });
      }
      // The client picks its send codec from the payload-types WE answer with,
      // and its offer never says which one it picked. The producer above is
      // therefore provisional; the arriving RTP is the only authority.
      const videoOffer = payload.video;

      const participant = {
        participantId,
        ownerParticipantId,
        source: payload.source,
        endpoint,
        presentation: Boolean(options.presentation),
        transport,
        producer,
        videoProducer,
        videoOffer,
        videoCodec,
        videoPaused: false,
        incoming: new Map()
      };
      room.participants.set(participantId, participant);
      if (videoProducer) {
        this.#watchProducerCodec(callId, participant);
      }
      if (!participant.presentation) {
        let disconnectPublished = false;
        const publishDisconnect = () => {
          if (disconnectPublished || participant.closing) {
            return;
          }
          disconnectPublished = true;
          for (const listener of this.#disconnectListeners) {
            listener({
              callId,
              participantId: ownerParticipantId,
              reason: 'transport_closed'
            });
          }
        };
        transport.on('icestatechange', (state) => {
          console.log(`ice call:${callId} participant:${ownerParticipantId} ` +
              `state:${state}`);
          if (state === 'disconnected' || state === 'closed') {
            publishDisconnect();
          }
        });
        transport.on('selectedtuplechange', (tuple) => {
          console.log(`ice-tuple call:${callId} ` +
              `participant:${ownerParticipantId} ` +
              `local:${tuple?.localAddress ?? tuple?.localIp}:${tuple?.localPort} ` +
              `remote:${tuple?.remoteIp}:${tuple?.remotePort}`);
        });
        transport.on('dtlsstatechange', (state) => {
          console.log(`dtls call:${callId} participant:${ownerParticipantId} ` +
              `state:${state}`);
          if (state === 'failed' || state === 'closed') {
            publishDisconnect();
          }
        });
      }

      try {
        for (const peer of room.participants.values()) {
          if (peer === participant) {
            continue;
          }
          const toPeer = await peer.transport.consume({
            producerId: producer.id,
            rtpCapabilities: this.#router.rtpCapabilities,
            paused: false,
            appData: { callId, producerParticipantId: participantId }
          });
          peer.incoming.set(participantId, {
            consumer: toPeer,
            source: firstEncodingSource(toPeer),
            video: await this.#consumeVideo(
                peer.transport, participant, callId)
          });

          const toParticipant = await transport.consume({
            producerId: peer.producer.id,
            rtpCapabilities: this.#router.rtpCapabilities,
            paused: false,
            appData: { callId, producerParticipantId: peer.participantId }
          });
          participant.incoming.set(peer.participantId, {
            consumer: toParticipant,
            source: firstEncodingSource(toParticipant),
            video: await this.#consumeVideo(transport, peer, callId)
          });
        }
      } catch (error) {
        room.participants.delete(participantId);
        transport.close();
        throw error;
      }

      const connection = {
        transport: {
          ufrag: transport.iceParameters.usernameFragment,
          pwd: transport.iceParameters.password,
          fingerprints: transport.dtlsParameters.fingerprints.map(
              telegramFingerprint),
          candidates: transport.iceCandidates.map(telegramCandidate)
        }
      };
      // "video" is a sibling of "transport" on the response root; the client
      // looks it up there (GroupJoinPayloadInternal.cpp:365).
      if (videoProducer) {
        connection.video = videoAnswer(
            endpoint, payload.source, this.#videoCodecs);
      }

      return {
        connection,
        canonicalSource: payload.source,
        viewerSources: this.getViewerSources(callId)
      };
    } catch (error) {
      transport.close();
      throw error;
    }
  }

  /**
   * Infer a participant's real video codec from the RTP it actually sends, and
   * correct the producer when it differs from the provisional one.
   *
   * Necessary because the join is a one-way negotiation: `videoAnswer` offers
   * every router codec, tgcalls picks one by ITS OWN preference order (an Apple
   * client commonly prefers H264), and the client's join payload carries only
   * SSRCs — no payload types. A producer built for the wrong codec still
   * matches by SSRC, so packets flow and consumers receive frames labelled with
   * the wrong codec: every receiver decodes nothing. The payload type on the
   * wire is the only signal that says what the client chose, and we already
   * know the PT->codec mapping because we allocated it in our own answer.
   */
  #watchProducerCodec(callId, participant) {
    // mediasoup discards a packet whose payload type matches no stream of the
    // producer, BEFORE any producer trace or stat exists, so the arriving
    // payload type cannot be read directly. What it does leave is an exact
    // signature: the transport keeps receiving bytes that the audio producer
    // does not account for, while the video producer reports no inbound stream
    // at all. That means "the client is sending video we are dropping", and the
    // codec is then found by elimination over the very list we offered.
    participant.codecProbe = this.#probeVideoCodec(callId, participant)
        .catch((error) => {
          participant.codecProbeFailed = error?.message ?? String(error);
        });
  }

  async #probeVideoCodec(callId, participant) {
    const tried = new Set([participant.videoCodec.toLowerCase()]);
    let swapped = false;
    let previous = await this.#videoTrafficSample(participant);

    for (let attempt = 0; attempt < CODEC_PROBE_ATTEMPTS; attempt++) {
      await delay(CODEC_PROBE_INTERVAL_MS);
      const room = this.#rooms.get(callId);
      if (!room || participant.closing ||
          room.participants.get(participant.participantId) !== participant) {
        return;
      }

      const current = await this.#videoTrafficSample(participant);
      if (current.videoStreams > 0) {
        // The producer is accepting RTP: this codec is the client's.
        if (swapped) {
          this.#publishSourcesChanged(callId, participant.ownerParticipantId,
              'video_codec_corrected');
        }
        return;
      }

      // Only rotate when undecodable video is demonstrably arriving. Without
      // this a participant that simply has its camera off would rotate through
      // every codec and then be on the wrong one when it finally sends.
      const unaccounted = (current.transportBytes - previous.transportBytes) -
          (current.audioBytes - previous.audioBytes);
      previous = current;
      if (unaccounted < CODEC_PROBE_MIN_UNACCOUNTED_BYTES) {
        continue;
      }

      const candidate = this.#videoCodecs.find(
          (entry) => !tried.has(entry.name.toLowerCase()));
      if (!candidate) {
        return;
      }
      tried.add(candidate.name.toLowerCase());
      await this.#swapVideoCodec(callId, participant, candidate.name);
      swapped = true;
      previous = await this.#videoTrafficSample(participant);
    }
  }

  /** Bytes the transport received, what audio accounts for, and whether the
   * video producer has any inbound stream at all. */
  async #videoTrafficSample(participant) {
    const [transportStats, audioStats, videoStats] = await Promise.all([
      participant.transport.getStats(),
      participant.producer.getStats(),
      participant.videoProducer
        ? participant.videoProducer.getStats()
        : Promise.resolve([])
    ]);
    return {
      transportBytes: sumStat(transportStats, 'bytesReceived'),
      audioBytes: sumStat(audioStats, 'byteCount'),
      videoStreams: videoStats.filter(
          (entry) => entry.type === 'inbound-rtp').length
    };
  }

  /**
   * Re-create one participant's video producer under the codec its RTP proves
   * it is sending, then re-create every consumer of it. Consumers carry
   * rewritten SSRCs, so this changes the per-viewer mapping and the control
   * plane is told to re-publish it.
   */
  async #swapVideoCodec(callId, participant, codecName) {
    const room = this.#rooms.get(callId);
    if (!room || participant.closing || participant.codecSwapInFlight ||
        room.participants.get(participant.participantId) !== participant) {
      return;
    }
    participant.codecSwapInFlight = true;
    try {
      const previous = participant.videoProducer;
      const paused = previous.paused;
      const appData = previous.appData;
      // The replacement reuses the same SSRCs, and mediasoup's RTP listener
      // refuses a duplicate SSRC, so the old producer must go first.
      previous.close();
      participant.videoProducer = await participant.transport.produce({
        kind: 'video',
        rtpParameters: videoProducerRtpParameters(participant.videoOffer,
            participant.participantId, codecName, this.#videoCodecs),
        paused,
        appData
      });
      participant.videoCodec = codecName;

      // Every viewer of this participant now consumes the replacement.
      for (const peer of room.participants.values()) {
        const incoming = peer.incoming.get(participant.participantId);
        if (!incoming) {
          continue;
        }
        incoming.video?.consumer.close();
        incoming.video = await this.#consumeVideo(
            peer.transport, participant, callId);
      }
      // The mapping change is published only once the replacement is proven to
      // be receiving (see #probeVideoCodec), so a rotation that guessed wrong
      // never makes the control plane republish.
    } catch (error) {
      // A failed correction must not take the call down: the participant keeps
      // its audio and its undecodable video, exactly as before the attempt.
      participant.codecSwapFailed = error?.message ?? String(error);
    } finally {
      participant.codecSwapInFlight = false;
    }
  }

  // Per-consumer video SSRCs are rewritten by mediasoup, so a viewer never sees
  // the producer's canonical layer SSRCs. Report the rewritten ones grouped the
  // way the client expects to receive them.
  async #consumeVideo(viewerTransport, producerParticipant, callId) {
    if (!producerParticipant.videoProducer) {
      return null;
    }
    const consumer = await viewerTransport.consume({
      producerId: producerParticipant.videoProducer.id,
      rtpCapabilities: this.#router.rtpCapabilities,
      paused: false,
      appData: {
        callId,
        producerParticipantId: producerParticipant.participantId
      }
    });

    const encodings = consumer.rtpParameters?.encodings ?? [];
    const layers = encodings
        .map((encoding) => encoding.ssrc)
        .filter((ssrc) => Number.isInteger(ssrc) && ssrc !== 0)
        .map((ssrc) => ssrc >>> 0);
    if (layers.length === 0) {
      throw new Error('mediasoup video consumer exposed no source');
    }

    const sourceGroups = [];
    if (layers.length > 1) {
      sourceGroups.push({ semantics: 'SIM', sources: layers });
    }
    for (const encoding of encodings) {
      if (encoding.rtx?.ssrc) {
        sourceGroups.push({
          semantics: 'FID',
          sources: [encoding.ssrc >>> 0, encoding.rtx.ssrc >>> 0]
        });
      }
    }
    if (sourceGroups.length === 0) {
      sourceGroups.push({ semantics: 'SIM', sources: layers });
    }

    return {
      consumer,
      endpoint: producerParticipant.endpoint,
      sourceGroups
    };
  }

  getViewerSources(callId) {
    const room = this.#rooms.get(callId);
    if (!room) {
      return {};
    }
    const result = {};
    for (const participant of room.participants.values()) {
      result[participant.participantId] = {};
      for (const [producerParticipantId, incoming] of participant.incoming) {
        result[participant.participantId][producerParticipantId] =
            incoming.source;
      }
    }
    return result;
  }

  // Structured per-viewer media. getViewerSources stays flat and audio-only so
  // audio fixtures keep replaying unchanged.
  getViewerMedia(callId) {
    const room = this.#rooms.get(callId);
    if (!room) {
      return {};
    }
    const result = {};
    for (const participant of room.participants.values()) {
      if (participant.presentation) {
        continue;
      }
      result[participant.ownerParticipantId] = {};
      for (const [producerParticipantId, incoming] of participant.incoming) {
        const producer = room.participants.get(producerParticipantId);
        if (!producer) {
          continue;
        }
        const producerId = producer.ownerParticipantId;
        const current = result[participant.ownerParticipantId][producerId] ?? {
          audioSource: incoming.source,
          video: null,
          presentation: null
        };
        if (producer.presentation) {
          current.presentation = incoming.video
            ? {
                endpoint: incoming.video.endpoint,
                sourceGroups: incoming.video.sourceGroups,
                paused: Boolean(producer.videoPaused)
              }
            : null;
        } else {
          current.audioSource = incoming.source;
          current.video = incoming.video
            ? {
                endpoint: incoming.video.endpoint,
                sourceGroups: incoming.video.sourceGroups,
                paused: Boolean(producer.videoPaused)
              }
            : null;
        }
        result[participant.ownerParticipantId][producerId] = current;
      }
    }
    return result;
  }

  async setVideoPaused(callId, participantId, paused) {
    const participant = this.#participant(callId, participantId);
    participant.videoPaused = Boolean(paused);
    if (!participant.videoProducer) {
      return;
    }
    if (paused) {
      await participant.videoProducer.pause();
    } else {
      await participant.videoProducer.resume();
    }
  }

  // The consuming side steering its own forwarded simulcast layer. This is what
  // the client's setRequestedVideoChannels quality maps onto.
  async setPreferredLayers(callId, viewerId, producerId, spatialLayer,
      temporalLayer) {
    const viewer = this.#participant(callId, viewerId);
    const incoming = viewer.incoming.get(producerId);
    if (!incoming?.video) {
      throw new Error(
          `viewer ${viewerId} has no video consumer for ${producerId}`);
    }
    await incoming.video.consumer.setPreferredLayers({
      spatialLayer,
      ...(temporalLayer === undefined ? {} : { temporalLayer })
    });
    return {
      preferredLayers: incoming.video.consumer.preferredLayers,
      currentLayers: incoming.video.consumer.currentLayers
    };
  }

  // Honor one viewer's ReceiverVideoConstraints for one producer. The layer
  // decision belongs here rather than in Ferrite: the constraint rides the
  // client's data channel to mediasoup and Ferrite's control plane never
  // touches the media transport. Returns null when there is nothing to steer
  // (no video consumer, or no layer has produced dimensions yet) so a malformed
  // or early message is ignored instead of tearing the channel down.
  async applyVideoConstraints(callId, viewerId, producerId, constraints) {
    const viewer = this.#participant(callId, viewerId);
    const incoming = viewer.incoming.get(producerId);
    if (!incoming?.video) {
      return null;
    }
    const producer =
        this.#rooms.get(callId)?.participants.get(producerId)?.videoProducer;
    if (!producer) {
      return null;
    }

    const layers = selectPreferredLayers(
        constraints, await this.#producerLayerDimensions(producer));
    if (!layers) {
      return null;
    }

    await incoming.video.consumer.setPreferredLayers(layers);
    return {
      requested: layers,
      preferredLayers: incoming.video.consumer.preferredLayers,
      currentLayers: incoming.video.consumer.currentLayers
    };
  }

  // Layer dimensions come from live RTP stats, not the join payload: the client
  // advertises SSRCs only. A layer that has not carried a packet yet reports no
  // dimensions and is skipped rather than guessed at.
  async #producerLayerDimensions(producer) {
    const stats = await producer.getStats();
    const encodings = producer.rtpParameters?.encodings ?? [];
    const layers = [];
    for (const stat of stats) {
      if (!Number.isFinite(stat.width) || !Number.isFinite(stat.height)) {
        continue;
      }
      const spatialLayer = encodings.findIndex(
          (encoding) => encoding.ssrc === stat.ssrc);
      if (spatialLayer < 0) {
        continue;
      }
      layers.push({ spatialLayer, width: stat.width, height: stat.height });
    }
    return layers;
  }

  async setMuted(callId, participantId, muted) {
    const participant = this.#participant(callId, participantId);
    if (muted) {
      await participant.producer.pause();
    } else {
      await participant.producer.resume();
    }
  }

  async leave(callId, participantId) {
    const room = this.#rooms.get(callId);
    const participant = room?.participants.get(participantId);
    if (!participant) {
      return false;
    }
    const presentationId = this.#presentationId(participantId);
    const presentation = room.participants.get(presentationId);
    if (presentation) {
      room.participants.delete(presentationId);
      presentation.closing = true;
      presentation.transport.close();
      for (const peer of room.participants.values()) {
        peer.incoming.delete(presentationId);
      }
    }
    room.participants.delete(participantId);
    participant.closing = true;
    participant.transport.close();
    for (const peer of room.participants.values()) {
      peer.incoming.delete(participantId);
    }
    return true;
  }

  async joinPresentation(callId, participantId, rawPayload) {
    this.#participant(callId, participantId);
    return await this.join(callId, this.#presentationId(participantId), rawPayload, {
      presentation: true,
      ownerParticipantId: participantId
    });
  }

  async leavePresentation(callId, participantId) {
    const room = this.#rooms.get(callId);
    const presentationId = this.#presentationId(participantId);
    const presentation = room?.participants.get(presentationId);
    if (!presentation) {
      return false;
    }
    room.participants.delete(presentationId);
    presentation.closing = true;
    presentation.transport.close();
    for (const peer of room.participants.values()) {
      peer.incoming.delete(presentationId);
    }
    return true;
  }

  isAlive(callId, participantId) {
    const room = this.#rooms.get(callId);
    const participant = room?.participants.get(participantId);
    return Boolean(participant && !participant.transport.closed &&
      ['connected', 'completed'].includes(participant.transport.iceState) &&
      participant.transport.dtlsState === 'connected');
  }

  async stats(callId) {
    const room = this.#rooms.get(callId);
    if (!room) {
      return { participants: {} };
    }
    const participants = {};
    for (const participant of room.participants.values()) {
      const consumers = {};
      for (const [peerId, incoming] of participant.incoming) {
        consumers[peerId] = {
          source: incoming.source,
          stats: await incoming.consumer.getStats(),
          video: incoming.video
            ? {
                endpoint: incoming.video.endpoint,
                sourceGroups: incoming.video.sourceGroups,
                preferredLayers: incoming.video.consumer.preferredLayers,
                currentLayers: incoming.video.consumer.currentLayers,
                stats: await incoming.video.consumer.getStats()
              }
            : null
        };
      }
      participants[participant.participantId] = {
        canonicalSource: participant.source,
        endpoint: participant.endpoint,
        presentation: participant.presentation,
        iceRole: participant.transport.iceRole,
        iceState: participant.transport.iceState,
        dtlsState: participant.transport.dtlsState,
        dtlsRole: participant.transport.dtlsParameters.role,
        producerPaused: participant.producer.paused,
        producerStats: await participant.producer.getStats(),
        videoProducer: participant.videoProducer
          ? {
              paused: participant.videoProducer.paused,
              // The codec actually negotiated for the arriving RTP.
              codec: participant.videoProducer.rtpParameters.codecs[0].mimeType,
              payloadType:
                  participant.videoProducer.rtpParameters.codecs[0].payloadType,
              encodings: participant.videoProducer.rtpParameters.encodings,
              stats: await participant.videoProducer.getStats()
            }
          : null,
        transportStats: await participant.transport.getStats(),
        consumers
      };
    }
    return { participants };
  }

  async health() {
    if (!this.#worker || this.#worker.closed || !this.#router ||
        this.#router.closed) {
      return { healthy: false };
    }
    return {
      healthy: true,
      pid: this.#worker.pid,
      rooms: this.#rooms.size,
      workerUsage: await this.#worker.getResourceUsage()
    };
  }

  // Snapshot the producer identities available to an internal RTP-tap
  // subscriber. Payload types deliberately stay out of this discovery result:
  // mediasoup chooses the consumer payload mapping when acquireRtpTap runs, and
  // that returned descriptor is the only safe input for an SDP generator.
  getRtpTapSources(callId) {
    const room = this.#rooms.get(callId);
    if (!room) {
      return [];
    }
    const sources = [];
    for (const participant of room.participants.values()) {
      if (participant.closing) {
        continue;
      }
      if (!participant.presentation && participant.producer) {
        sources.push({
          participantId: participant.ownerParticipantId,
          presentation: false,
          kind: 'audio',
          endpoint: participant.endpoint
        });
      }
      if (participant.videoProducer && !participant.videoPaused) {
        sources.push({
          participantId: participant.ownerParticipantId,
          presentation: participant.presentation,
          kind: 'video',
          endpoint: participant.endpoint,
          codec: participant.videoProducer.rtpParameters.codecs[0]?.mimeType
        });
      }
    }
    return sources;
  }

  // Reference-counted consumers are addressed by subscriber id. Broadcast and
  // recording use different ids and therefore share the router's producer tap
  // mechanism without sharing lifecycle. Each target is an explicit UDP sink;
  // no public media address or credential is inferred by the worker.
  async acquireRtpTap(callId, subscriberId, targets) {
    const room = this.#rooms.get(callId);
    if (!room) {
      throw new Error(`room ${callId} is missing`);
    }
    if (typeof subscriberId !== 'string' || subscriberId.length === 0 ||
        subscriberId.length > 128) {
      throw new TypeError('tap subscriber id has invalid length');
    }
    if (room.taps.has(subscriberId)) {
      return room.taps.get(subscriberId).descriptors;
    }
    if (!Array.isArray(targets) || targets.length === 0 || targets.length > 64) {
      throw new TypeError('tap targets must contain 1..64 entries');
    }

    const entries = [];
    try {
      for (const target of targets) {
        requireObject(target, 'tap target');
        const participantId = target.presentation
          ? this.#presentationId(target.participantId)
          : target.participantId;
        const participant = room.participants.get(participantId);
        if (!participant) {
          throw new Error(`tap participant ${target.participantId} is not joined`);
        }
        const producer = target.kind === 'audio'
          ? participant.producer
          : target.kind === 'video'
            ? participant.videoProducer
            : null;
        if (!producer) {
          throw new Error(
              `tap participant ${target.participantId} has no ${target.kind} producer`);
        }
        if (typeof target.ip !== 'string' || target.ip.length === 0 ||
            !Number.isInteger(target.port) || target.port < 1 ||
            target.port > 65535) {
          throw new TypeError('tap target endpoint is invalid');
        }
        const listenInfo = {
          protocol: 'udp',
          ip: this.#listenIp,
          portRange: { min: this.#rtcMinPort, max: this.#rtcMaxPort }
        };
        const transport = await this.#router.createPlainTransport({
          listenInfo,
          rtcpMux: true,
          comedia: false
        });
        await transport.connect({ ip: target.ip, port: target.port });
        const consumer = await transport.consume({
          producerId: producer.id,
          rtpCapabilities: this.#router.rtpCapabilities,
          // FFmpeg's RTP demuxer does not negotiate retransmission payloads.
          // Keep the internal tap as one continuous primary stream instead of
          // sending an unmapped RTX SSRC/PT into the same UDP session.
          enableRtx: false,
          preferredLayers: target.kind === 'video'
            ? { spatialLayer: 0, temporalLayer: 0 }
            : undefined,
          paused: Boolean(target.paused),
          appData: {
            callId,
            subscriberId,
            participantId: target.participantId,
            presentation: Boolean(target.presentation)
          }
        });
        entries.push({
          transport,
          consumer,
          descriptor: {
            participantId: target.participantId,
            presentation: Boolean(target.presentation),
            kind: target.kind,
            endpoint: participant.endpoint,
            localTuple: transport.tuple,
            remoteTuple: transport.rtcpTuple ?? null,
            rtpParameters: consumer.rtpParameters
          }
        });
      }
      const state = {
        entries,
        descriptors: entries.map((entry) => entry.descriptor)
      };
      room.taps.set(subscriberId, state);
      return state.descriptors;
    } catch (error) {
      for (const entry of entries) {
        entry.transport.close();
      }
      throw error;
    }
  }

  releaseRtpTap(callId, subscriberId) {
    const room = this.#rooms.get(callId);
    const tap = room?.taps.get(subscriberId);
    if (!tap) {
      return false;
    }
    room.taps.delete(subscriberId);
    for (const entry of tap.entries) {
      entry.transport.close();
    }
    return true;
  }

  async resumeRtpTap(callId, subscriberId) {
    const tap = this.#rooms.get(callId)?.taps.get(subscriberId);
    if (!tap) {
      return false;
    }
    for (const entry of tap.entries) {
      await entry.consumer.resume();
    }
    return true;
  }

  async requestRtpTapKeyFrames(callId, subscriberId) {
    const tap = this.#rooms.get(callId)?.taps.get(subscriberId);
    if (!tap) {
      return false;
    }
    for (const entry of tap.entries) {
      if (entry.consumer.kind === 'video') {
        await entry.consumer.requestKeyFrame();
      }
    }
    return true;
  }

  async endRoom(callId) {
    const room = this.#rooms.get(callId);
    if (!room) {
      return false;
    }
    this.#rooms.delete(callId);
    for (const tap of room.taps.values()) {
      for (const entry of tap.entries) {
        entry.transport.close();
      }
    }
    for (const participant of room.participants.values()) {
      participant.closing = true;
      participant.transport.close();
    }
    return true;
  }

  async killWorkerForTest() {
    if (!this.#worker || this.#worker.closed) {
      return;
    }
    process.kill(this.#worker.pid, 'SIGKILL');
  }

  async close() {
    for (const callId of [...this.#rooms.keys()]) {
      await this.endRoom(callId);
    }
    this.#router?.close();
    this.#worker?.close();
  }

  #participant(callId, participantId) {
    const participant = this.#rooms.get(callId)?.participants.get(participantId);
    if (!participant) {
      throw new Error(`participant ${participantId} is not joined`);
    }
    return participant;
  }

  #presentationId(participantId) {
    return `${participantId}\u0000presentation`;
  }
}
