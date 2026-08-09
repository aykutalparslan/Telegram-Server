// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Collections.Concurrent;

namespace Ferrite.Services.Calls;

/// <summary>
/// The live per-viewer media mapping for every active call: which rewritten
/// SSRCs one viewer receives for one producer.
///
/// This is deliberately NOT persisted. mediasoup rewrites per-consumer SSRCs
/// and re-derives them on every join, leave, and presentation
/// change, so the worker — not the database — is the authority. Persisting a
/// snapshot would let a restarted worker hand clients SSRCs that no longer
/// exist. Losing this map instead degrades a participant row to its canonical
/// source, which is the same fallback a row already takes before its media
/// mapping arrives.
///
/// Keys on both levels are <c>groupCallParticipantState.media_id</c>, the durable
/// media correlation id; callers translate user ids through the participant rows.
/// </summary>
public sealed class GroupCallMediaSourceMap
{
    private readonly ConcurrentDictionary<long, CallMap> _calls = new();

    private sealed class CallMap
    {
        public ConcurrentDictionary<string,
            IReadOnlyDictionary<string, GroupCallViewerSources>> Viewers { get; } = new();
    }

    /// <summary>
    /// Replaces the whole mapping for one call. The media plane returns the full
    /// viewer/producer matrix on every join, so a wholesale replace is what keeps
    /// the map consistent with the worker rather than accumulating stale rows.
    /// </summary>
    public void Replace(long callId,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, GroupCallViewerSources>>
            viewerSources)
    {
        if (viewerSources.Count == 0)
        {
            return;
        }

        var map = new CallMap();
        foreach ((string viewerId, var producers) in viewerSources)
        {
            map.Viewers[viewerId] = producers;
        }
        _calls[callId] = map;
    }

    /// <summary>
    /// What <paramref name="viewerMediaId"/> receives for
    /// <paramref name="producerMediaId"/>, or null when no mapping exists — which
    /// makes the builder fall back to the canonical source and omit video rows
    /// rather than publish SSRCs the viewer cannot receive.
    /// </summary>
    public GroupCallViewerSources? TryGet(long callId, string? viewerMediaId,
        string? producerMediaId)
    {
        if (string.IsNullOrEmpty(viewerMediaId) || string.IsNullOrEmpty(producerMediaId) ||
            !_calls.TryGetValue(callId, out CallMap? map) ||
            !map.Viewers.TryGetValue(viewerMediaId, out var producers))
        {
            return null;
        }

        return producers.TryGetValue(producerMediaId, out GroupCallViewerSources? sources)
            ? sources
            : null;
    }

    /// <summary>
    /// Drops one participant from the map after it leaves. The next join replaces
    /// the whole call anyway; this keeps a long-lived call from serving a departed
    /// participant's rows in the meantime.
    /// </summary>
    public void RemoveParticipant(long callId, string mediaId)
    {
        if (string.IsNullOrEmpty(mediaId) || !_calls.TryGetValue(callId, out CallMap? map))
        {
            return;
        }

        map.Viewers.TryRemove(mediaId, out _);
        foreach (string viewerId in map.Viewers.Keys)
        {
            if (!map.Viewers.TryGetValue(viewerId, out var producers) ||
                !producers.ContainsKey(mediaId))
            {
                continue;
            }

            var trimmed = new Dictionary<string, GroupCallViewerSources>(producers);
            trimmed.Remove(mediaId);
            map.Viewers[viewerId] = trimmed;
        }
    }

    /// <summary>
    /// Drops one producer's SCREEN SHARE from every viewer's mapping, leaving its
    /// camera stream untouched. Tearing down a presentation transport does not
    /// produce a fresh viewer matrix — the media plane only reports one — so
    /// without this the next row built for a viewer would still carry screen SSRCs
    /// the worker has already stopped forwarding.
    /// </summary>
    public void RemoveProducerPresentation(long callId, string mediaId)
    {
        if (string.IsNullOrEmpty(mediaId) || !_calls.TryGetValue(callId, out CallMap? map))
        {
            return;
        }

        foreach (string viewerId in map.Viewers.Keys)
        {
            if (!map.Viewers.TryGetValue(viewerId, out var producers) ||
                !producers.TryGetValue(mediaId, out GroupCallViewerSources? sources) ||
                sources.Presentation == null)
            {
                continue;
            }

            var updated = new Dictionary<string, GroupCallViewerSources>(producers)
            {
                [mediaId] = sources with { Presentation = null },
            };
            map.Viewers[viewerId] = updated;
        }
    }

    /// <summary>Releases a call's mapping when the call ends.</summary>
    public void Forget(long callId) => _calls.TryRemove(callId, out _);
}
