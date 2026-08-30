// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Collections.Concurrent;

namespace Ferrite.Services.Calls;

public sealed class GroupCallMediaSourceMap
{
    private readonly ConcurrentDictionary<long, CallMap> _calls = new();

    private sealed class CallMap
    {
        public ConcurrentDictionary<string,
            IReadOnlyDictionary<string, GroupCallViewerSources>> Viewers { get; } = new();
    }

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

    public void Forget(long callId) => _calls.TryRemove(callId, out _);
}
