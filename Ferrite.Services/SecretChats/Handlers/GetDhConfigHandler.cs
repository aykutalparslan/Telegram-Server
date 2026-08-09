// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Crypto;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.SecretChats.Handlers;

public sealed class GetDhConfigHandler
{
    public const int MinimumRandomLength = 0;
    public const int MaximumRandomLength = 256;

    private readonly IRandomGenerator _random;

    public GetDhConfigHandler(IRandomGenerator random)
    {
        _random = random;
    }

    [TLFunction(Constructors.baseLayer_GetDhConfig)]
    public ValueTask<TLDhConfig> Handle(long authKeyId, TLBytes q)
    {
        var request = (GetDhConfig)q;
        int version = request.Version;
        int randomLength = request.RandomLength;

        if (randomLength is < MinimumRandomLength or > MaximumRandomLength)
        {
            return ValueTask.FromResult((TLDhConfig)RpcErrorGenerator
                .GenerateError(400, "RANDOM_LENGTH_INVALID"u8));
        }

        byte[] random = _random.GetRandomBytes(randomLength);
        if (version == TelegramDhParameters.SecretChatVersion)
        {
            return ValueTask.FromResult<TLDhConfig>(DhConfigNotModified.Builder()
                .Random(random)
                .Build());
        }

        return ValueTask.FromResult<TLDhConfig>(DhConfig.Builder()
            .G(TelegramDhParameters.SecretChatGenerator)
            .P(TelegramDhParameters.Prime)
            .Version(TelegramDhParameters.SecretChatVersion)
            .Random(random)
            .Build());
    }
}
