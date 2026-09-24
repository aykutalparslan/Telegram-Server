// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.Schema;
using Ferrite.Utils;

namespace Ferrite.Services.Auth;

public readonly record struct ClientLayerMigrationResult(
    int Migrated, int Preserved, int Invalid);

public sealed class ClientLayerMigration
{
    private readonly IAppInfoRepository _appInfos;
    private readonly IAuthorizationRepository _authorizations;
    private readonly IClientLayerRepository _clientLayers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger _log;

    public ClientLayerMigration(IAppInfoRepository appInfos,
        IAuthorizationRepository authorizations,
        IClientLayerRepository clientLayers,
        IUnitOfWork unitOfWork,
        ILogger log)
    {
        _appInfos = appInfos;
        _authorizations = authorizations;
        _clientLayers = clientLayers;
        _unitOfWork = unitOfWork;
        _log = log;
    }

    public async ValueTask<ClientLayerMigrationResult> RunAsync()
    {
        var existing = new Dictionary<long, int>();
        int preserved = 0;
        int invalid = 0;
        foreach (TLClientLayer layer in _clientLayers.GetClientLayers())
        {
            using (layer)
            {
                var value = layer.AsClientLayer();
                existing[value.AuthKeyId] = value.ApiLayer;
                if (LayerSchemaCatalog.Layers.Contains(value.ApiLayer))
                {
                    preserved++;
                }
                else
                {
                    invalid++;
                    _log.Warning($"Invalid stored client layer: auth_key_id=" +
                                 $"{value.AuthKeyId} api_layer={value.ApiLayer}");
                }
            }
        }

        var candidates = new HashSet<long>();
        foreach (TLAppInfo app in _appInfos.GetAppInfos())
        {
            using (app)
            {
                candidates.Add(app.AsAppInfo().AuthKeyId);
            }
        }
        foreach (TLAuthInfo authorization in _authorizations.GetAuthorizations())
        {
            using (authorization)
            {
                candidates.Add(authorization.AsAuthInfo().AuthKeyId);
            }
        }

        int migrated = 0;
        foreach (long authKeyId in candidates)
        {
            if (existing.ContainsKey(authKeyId))
            {
                continue;
            }

            using TLClientLayer row = ClientLayer.Builder()
                .AuthKeyId(authKeyId)
                .ApiLayer(214)
                .Build();
            if (!_clientLayers.PutClientLayer(row))
            {
                throw new InvalidOperationException(
                    $"Unable to backfill client layer for auth key {authKeyId}.");
            }
            migrated++;
        }

        if (migrated != 0 && !await _unitOfWork.SaveAsync())
        {
            throw new InvalidOperationException("Unable to commit client layer backfill.");
        }

        _log.Information($"Client layer migration: migrated={migrated} " +
                         $"preserved={preserved} invalid={invalid}");
        return new(migrated, preserved, invalid);
    }
}
