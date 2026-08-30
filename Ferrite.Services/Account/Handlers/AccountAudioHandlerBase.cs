// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Handlers.AccountMethods;

public abstract class AccountAudioHandlerBase
{
    protected readonly AccountAudioStore Store;
    private readonly ProfileStore _profiles;

    protected AccountAudioHandlerBase(AccountAudioStore store,
        ProfileStore profiles)
    {
        Store = store;
        _profiles = profiles;
    }

    protected ValueTask<long?> GetUserIdAsync(long authKeyId) =>
        _profiles.GetUserIdAsync(authKeyId);

    protected static bool TryReadDocument(InputDocumentView view,
        out AudioDocumentInput input)
    {
        if (view.Is(out InputDocument document) && document.Id != 0)
        {
            input = new AudioDocumentInput(document.Id, document.AccessHash,
                document.FileReference.ToArray());
            return true;
        }
        input = default;
        return false;
    }

    protected static bool TryReadUser(InputUserView view,
        out AudioUserInput input)
    {
        if (view.Is(out InputUserSelf _))
        {
            input = new AudioUserInput(AudioUserInputKind.Self, 0, 0);
            return true;
        }
        if (view.Is(out InputUser user) && user.UserId > 0)
        {
            input = new AudioUserInput(AudioUserInputKind.User, user.UserId,
                user.AccessHash);
            return true;
        }
        if (view.Is(out InputUserFromMessage from) && from.UserId > 0)
        {
            input = new AudioUserInput(AudioUserInputKind.FromMessage,
                from.UserId, 0);
            return true;
        }
        input = default;
        return false;
    }

    protected static TLBytes AuthError() =>
        RpcErrorGenerator.GenerateError(401, "AUTH_KEY_INVALID"u8);
    protected static TLBytes DocumentError() =>
        RpcErrorGenerator.GenerateError(400, "DOCUMENT_INVALID"u8);
    protected static TLBytes UserError() =>
        RpcErrorGenerator.GenerateError(400, "USER_ID_INVALID"u8);
}
