// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

/**
 * Pick the highest simulcast layer that fits the consumer's requested bounds.
 * tgcalls sends ReceiverVideoConstraints on its data channel; without honoring
 * it every consumer receives the top layer and bandwidth is unusable above
 * roughly four participants.
 *
 * A malformed or absent constraint is ignored rather than throwing: a bad
 * message must not tear down the data channel, and "no constraint" legitimately
 * means "send me the best layer". A producer forwarding one layer (VP9, which
 * mediasoup refuses to simulcast over SSRCs) always resolves to that layer.
 *
 * @param {{maxWidth?: number, maxHeight?: number}|null|undefined} constraints
 * @param {Array<{spatialLayer: number, width: number, height: number}>} producerLayers
 * @returns {{spatialLayer: number, temporalLayer: number}|null}
 */
export function selectPreferredLayers(constraints, producerLayers) {
  if (!Array.isArray(producerLayers) || producerLayers.length === 0) {
    return null;
  }

  const sorted = [...producerLayers].sort(
      (a, b) => a.spatialLayer - b.spatialLayer);
  const maxWidth = positiveBound(constraints?.maxWidth);
  const maxHeight = positiveBound(constraints?.maxHeight);

  // Lowest layer is the floor: a viewer too small for even that still needs
  // video, so it receives the cheapest stream rather than none.
  let chosen = sorted[0];
  for (const layer of sorted) {
    if (layer.width <= maxWidth && layer.height <= maxHeight) {
      chosen = layer;
    }
  }

  return { spatialLayer: chosen.spatialLayer, temporalLayer: 2 };
}

function positiveBound(value) {
  return Number.isFinite(value) && value > 0 ? value : Number.MAX_SAFE_INTEGER;
}
