// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;

namespace Ferrite.Services.Handlers.MessageMethods;

/// <summary>
/// Ferrite has no legacy server-notification-id queue. Acknowledging received
/// message ids is therefore a deliberately empty, read-only operation.
/// </summary>
public sealed class ReceivedMessagesHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;

    public ReceivedMessagesHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
    }

    [TLFunction(Constructors.baseLayer_ReceivedMessages)]
    public async ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        using var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
        {
            return RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);
        }

        var acknowledgements = new Vector();
        byte[] result = acknowledgements.ToReadOnlySpan().ToArray();
        return new TLBytes(result, 0, result.Length);
    }
}
