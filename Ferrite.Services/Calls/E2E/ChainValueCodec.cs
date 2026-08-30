// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.e2eChain.e2e;

namespace Ferrite.Services.Calls.E2E;

public static class ChainValueCodec
{
    public static byte[] SerializeGroupState(ChainGroupStateValue groupState)
    {
        var participants = new VectorBare();
        foreach (var participant in groupState.Participants)
        {
            byte[] encoded;
            {
                using var built = new ChainGroupParticipant(participant.UserId,
                    participant.PublicKey, new Flags(participant.Flags), false, false,
                    participant.Version);
                encoded = built.ToReadOnlySpan().ToArray();
            }
            participants.Append(encoded);
        }

        using var state = ChainGroupState.Builder()
            .Participants(participants)
            .ExternalPermissions(groupState.ExternalPermissions)
            .Build();
        return state.ToReadOnlySpan().ToArray();
    }

    public static byte[] SerializeSharedKey(ChainSharedKeyValue sharedKey)
    {
        var users = new VectorBareOfLong();
        foreach (long userId in sharedKey.DestUserId) users.Append(userId);

        var headers = new VectorBareOfString();
        foreach (byte[] header in sharedKey.DestHeader) headers.Append(header);

        using var key = ChainSharedKey.Builder()
            .Ek(sharedKey.Ek)
            .EncryptedSharedKey(sharedKey.EncryptedSharedKey)
            .DestUserId(users)
            .DestHeader(headers)
            .Build();
        return key.ToReadOnlySpan().ToArray();
    }

    public static bool TryReadGroupState(Span<byte> span, out ChainGroupStateValue groupState)
    {
        groupState = null!;
        var view = new ChainGroupStateView(span);
        if (!view.Is(out ChainGroupState state)) return false;

        var participants = new List<ChainParticipant>();
        var vector = state.Participants;
        for (int i = 0; i < vector.Count; i++)
        {
            var participantView = new ChainGroupParticipantView(vector.ReadTLObject());
            if (!participantView.Is(out ChainGroupParticipant participant)) return false;
            participants.Add(new ChainParticipant(
                participant.UserId,
                participant.PublicKey.ToArray(),
                participant.Flags.ToInt(),
                participant.Version));
        }

        groupState = new ChainGroupStateValue(participants, state.ExternalPermissions);
        return true;
    }

    public static bool TryReadSharedKey(Span<byte> span, out ChainSharedKeyValue sharedKey)
    {
        sharedKey = null!;
        var view = new ChainSharedKeyView(span);
        if (!view.Is(out ChainSharedKey key)) return false;

        var destUserId = new List<long>();
        var users = key.DestUserId;
        for (int i = 0; i < users.Count; i++)
        {
            destUserId.Add(users[i]);
        }

        var destHeader = new List<byte[]>();
        var headers = key.DestHeader;
        for (int i = 0; i < headers.Count; i++)
        {
            destHeader.Add(headers[i].ToArray());
        }

        sharedKey = new ChainSharedKeyValue(
            key.Ek.ToArray(), key.EncryptedSharedKey.ToArray(), destUserId, destHeader);
        return true;
    }
}
