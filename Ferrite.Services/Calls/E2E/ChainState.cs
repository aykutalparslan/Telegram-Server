// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Crypto;

namespace Ferrite.Services.Calls.E2E;

public sealed class ChainState
{
    public ChainState(ChainKeyValueState keyValueState, ChainGroupStateValue groupState,
        ChainSharedKeyValue sharedKey)
    {
        KeyValueState = keyValueState;
        GroupState = groupState;
        SharedKey = sharedKey;
    }

    public ChainKeyValueState KeyValueState { get; private set; }
    public ChainGroupStateValue GroupState { get; private set; }
    public ChainSharedKeyValue SharedKey { get; private set; }

    private bool _hasSetValue;
    private bool _hasGroupStateChange;
    private bool _hasSharedKeyChange;

    public static ChainState CreateEmpty() =>
        new(ChainKeyValueState.Empty(), ChainGroupStateValue.Empty, ChainSharedKeyValue.EmptyKey);

    public ChainState Clone() => new(KeyValueState.Clone(), GroupState, SharedKey);

    public ChainValidationError Apply(ChainBlockValue block)
    {
        if (block.Height == 0)
        {
            GroupState = new ChainGroupStateValue(
                Array.Empty<ChainParticipant>(), ChainPermissionFlags.AllPermissions);
        }

        byte[]? signerPublicKey = block.SignaturePublicKey;
        if (signerPublicKey == null && GroupState.Participants.Count > 0)
        {
            signerPublicKey = GroupState.Participants[0].PublicKey;
        }
        if (signerPublicKey == null)
        {
            return ChainValidationError.InvalidBlock;
        }

        byte[] preimage = ChainBlockCodec.SerializeWithZeroSignature(block);
        if (!Ed25519Verifier.Verify(signerPublicKey, preimage, block.Signature))
        {
            return ChainValidationError.InvalidSignature;
        }

        _hasSetValue = false;
        _hasSharedKeyChange = false;
        _hasGroupStateChange = false;
        foreach (var change in block.Changes)
        {
            var error = ApplyChange(change, signerPublicKey);
            if (error != ChainValidationError.None) return error;
        }

        return ValidateState(block.StateProof);
    }

    private ChainValidationError ApplyChange(ChainChangeValue change, byte[] signerPublicKey)
    {
        switch (change)
        {
            case ChainChangeNoopValue:
                return ChainValidationError.None;

            case ChainChangeSetValueValue setValue:
            {
                _hasSetValue = true;
                var permissions = GroupState.GetPermissions(
                    signerPublicKey, ChainPermissionFlags.AllPermissions);
                if (!permissions.MaySetValue) return ChainValidationError.NoPermissions;
                try
                {
                    KeyValueState.SetValue(setValue.Key, setValue.Value);
                }
                catch (ChainCodecException)
                {
                    return ChainValidationError.InvalidBlock;
                }
                return ChainValidationError.None;
            }

            case ChainChangeSetGroupStateValue setGroupState:
            {
                _hasGroupStateChange = true;
                var permissions = GroupState.GetPermissions(
                    signerPublicKey, ChainPermissionFlags.AllPermissions);
                var error = SetGroupState(setGroupState.GroupState, permissions);
                if (error != ChainValidationError.None) return error;

                var afterPermissions = GroupState.GetPermissions(
                    signerPublicKey, ChainPermissionFlags.AllPermissions);
                if (!afterPermissions.MayChangeSharedKey) return ChainValidationError.NoPermissions;
                SharedKey = ChainSharedKeyValue.EmptyKey;
                return ChainValidationError.None;
            }

            case ChainChangeSetSharedKeyValue setSharedKey:
            {
                _hasSharedKeyChange = true;
                var permissions = GroupState.GetPermissions(
                    signerPublicKey, ChainPermissionFlags.AllPermissions);
                return SetSharedKey(setSharedKey.SharedKey, permissions);
            }

            default:
                return ChainValidationError.InvalidBlock;
        }
    }

    public static ChainValidationError ValidateGroupState(ChainGroupStateValue groupState)
    {
        var userIds = new HashSet<long>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var participant in groupState.Participants)
        {
            userIds.Add(participant.UserId);
            keys.Add(Convert.ToHexString(participant.PublicKey));
            if ((participant.Flags & ~ChainPermissionFlags.AllPermissions) != 0)
            {
                return ChainValidationError.InvalidGroupState;
            }
        }
        if ((groupState.ExternalPermissions & ~ChainPermissionFlags.AllPermissions) != 0)
        {
            return ChainValidationError.InvalidGroupState;
        }
        if (userIds.Count != groupState.Participants.Count)
        {
            return ChainValidationError.InvalidGroupState;
        }
        if (keys.Count != groupState.Participants.Count)
        {
            return ChainValidationError.InvalidGroupState;
        }
        return ChainValidationError.None;
    }

    private ChainValidationError SetGroupState(ChainGroupStateValue groupState,
        ChainPermissions permissions)
    {
        var error = ValidateGroupState(groupState);
        if (error != ChainValidationError.None) return error;

        var old = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var participant in GroupState.Participants)
        {
            old[ParticipantKey(participant)] = participant.Flags;
        }
        var proposed = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var participant in groupState.Participants)
        {
            proposed[ParticipantKey(participant)] = participant.Flags;
        }

        if ((~GroupState.ExternalPermissions & groupState.ExternalPermissions) != 0)
        {
            return ChainValidationError.NoPermissions;
        }

        int neededFlags = 0;
        foreach (var entry in old)
        {
            if (!proposed.ContainsKey(entry.Key) && !permissions.MayRemoveUsers)
            {
                return ChainValidationError.NoPermissions;
            }
        }
        foreach (var entry in proposed)
        {
            if (!old.TryGetValue(entry.Key, out int oldFlags))
            {
                if (!permissions.MayAddUsers) return ChainValidationError.NoPermissions;
                neededFlags |= entry.Value;
            }
            else if (entry.Value != oldFlags)
            {
                if (!permissions.MayAddUsers || !permissions.MayRemoveUsers)
                {
                    return ChainValidationError.NoPermissions;
                }
                neededFlags |= entry.Value & ~oldFlags;
            }
        }

        int missingFlags = neededFlags &
            ~(permissions.Flags & ChainPermissionFlags.AllPermissions);
        if (missingFlags != 0) return ChainValidationError.NoPermissions;

        GroupState = groupState;
        return ChainValidationError.None;
    }

    private static string ParticipantKey(ChainParticipant participant) =>
        participant.UserId.ToString() + ":" + Convert.ToHexString(participant.PublicKey);

    public static ChainValidationError ValidateSharedKey(ChainSharedKeyValue sharedKey,
        ChainGroupStateValue groupState)
    {
        if (sharedKey.IsEmpty) return ChainValidationError.None;
        if (sharedKey.DestUserId.Count != sharedKey.DestHeader.Count)
        {
            return ChainValidationError.InvalidSharedSecret;
        }
        if (sharedKey.DestUserId.Count != groupState.Participants.Count)
        {
            return ChainValidationError.InvalidSharedSecret;
        }
        var users = new HashSet<long>(sharedKey.DestUserId);
        if (users.Count != sharedKey.DestUserId.Count)
        {
            return ChainValidationError.InvalidSharedSecret;
        }
        foreach (var participant in groupState.Participants)
        {
            if (!users.Contains(participant.UserId))
            {
                return ChainValidationError.InvalidSharedSecret;
            }
        }
        return ChainValidationError.None;
    }

    private ChainValidationError SetSharedKey(ChainSharedKeyValue sharedKey,
        ChainPermissions permissions)
    {
        if (!SharedKey.IsEmpty) return ChainValidationError.InvalidBlock;
        if (!permissions.MayChangeSharedKey) return ChainValidationError.NoPermissions;

        var error = ValidateSharedKey(sharedKey, GroupState);
        if (error != ChainValidationError.None) return error;

        SharedKey = sharedKey;
        return ChainValidationError.None;
    }

    public ChainValidationError ValidateState(ChainStateProofValue stateProof)
    {
        if (!stateProof.KvHash.AsSpan().SequenceEqual(KeyValueState.Hash))
        {
            return ChainValidationError.InvalidBlock;
        }

        if (!_hasGroupStateChange && !_hasSetValue)
        {
            return ChainValidationError.NoChanges;
        }

        if (_hasGroupStateChange && stateProof.GroupState != null)
        {
            return ChainValidationError.InvalidStateProofGroup;
        }
        if (!_hasGroupStateChange && stateProof.GroupState == null)
        {
            return ChainValidationError.InvalidStateProofGroup;
        }
        if (!_hasGroupStateChange && !stateProof.GroupState!.Equals(GroupState))
        {
            return ChainValidationError.InvalidStateProofGroup;
        }

        bool sharedKeyMustBeOmitted = _hasGroupStateChange || _hasSharedKeyChange;
        if (sharedKeyMustBeOmitted && stateProof.SharedKey != null)
        {
            return ChainValidationError.InvalidStateProofSecret;
        }
        if (!sharedKeyMustBeOmitted && stateProof.SharedKey == null)
        {
            return ChainValidationError.InvalidStateProofSecret;
        }
        if (!sharedKeyMustBeOmitted && !stateProof.SharedKey!.Equals(SharedKey))
        {
            return ChainValidationError.InvalidStateProofSecret;
        }

        var error = ValidateGroupState(GroupState);
        if (error != ChainValidationError.None) return error;
        return ValidateSharedKey(SharedKey, GroupState);
    }
}
