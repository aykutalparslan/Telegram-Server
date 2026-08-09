// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Autofac;
using Autofac.Features.Indexed;
using Ferrite.Core.Execution.Functions.BaseLayer;
using Ferrite.Core.Execution.Functions;
using Ferrite.Crypto;
using Ferrite.Data.Repositories;
using Ferrite.Services;
using Ferrite.Core.Execution;
using Ferrite.Core.RequestChain;
using Ferrite.TL;
using Ferrite.TL.mtproto;
using Ferrite.Utils;

namespace Ferrite.Core.Execution;

public class ExecutionEngine : IExecutionEngine
{
    private readonly IIndex<FunctionKey, ITLFunction> _functions;
    private readonly IIndex<FunctionKey, ITLStreamingFunction> _streamingFunctions;
    private readonly IIndex<FunctionKey, ITLFileFunction> _fileFunctions;
    private readonly IMTProtoService _mtproto;
    private readonly IAuthService _auth;
    private readonly IRandomGenerator _random;
    private readonly ILogger _log;
    private readonly IWriteBatchAccessor? _writeBatches;
    private readonly IAccountSettingsRepository? _accountSettings;
    private readonly TimeProvider _time;
    private readonly AsyncLocal<int> _writeScopeDepth = new();

    public ExecutionEngine(IIndex<FunctionKey, ITLFunction> functions,
        IIndex<FunctionKey, ITLStreamingFunction> streamingFunctions,
        IIndex<FunctionKey, ITLFileFunction> fileFunctions,
        IMTProtoService mtproto, IAuthService auth, IRandomGenerator random, ILogger log,
        IWriteBatchAccessor? writeBatches = null,
        IAccountSettingsRepository? accountSettings = null,
        TimeProvider? timeProvider = null)
    {
        _functions = functions;
        _streamingFunctions = streamingFunctions;
        _fileFunctions = fileFunctions;
        _mtproto = mtproto;
        _auth = auth;
        _random = random;
        _log = log;
        _writeBatches = writeBatches;
        _accountSettings = accountSettings;
        _time = timeProvider ?? TimeProvider.System;
    }

    public async ValueTask<TLBytes?> Invoke(TLBytes rpc, TLExecutionContext ctx, int layer = IExecutionEngine.DefaultLayer)
    {
        if (_writeBatches == null || _writeScopeDepth.Value != 0)
        {
            return await InvokeCore(rpc, ctx, layer);
        }

        _writeScopeDepth.Value++;
        try
        {
            using IWriteBatchScope scope = _writeBatches.BeginScope();
            return await InvokeCore(rpc, ctx, layer);
        }
        finally
        {
            _writeScopeDepth.Value--;
        }
    }

    private async ValueTask<TLBytes?> InvokeCore(TLBytes rpc, TLExecutionContext ctx, int layer)
    {
        if (rpc.Constructor == Constructors.mtproto_GzipPacked)
        {
            using var unpacked = GzipPackedHelper.Unpack(rpc);
            return await InvokeCore(unpacked, ctx, layer);
        }

        var authError = await GetAuthError(rpc.Constructor, ctx);
        if (authError != null) return authError;

        try
        {
            var found = _functions.TryGetValue(new FunctionKey(layer, rpc.Constructor), out var func);
            if (!found)
            {
                _log.Error($"#{rpc.Constructor.ToString("x")} is not found for layer {layer}");
                var err = RpcErrorGenerator.GenerateError(500, "INTERNAL_SERVER_ERROR"u8);
                return RpcResultGenerator.Generate(err, ctx.MessageId);
            }
            return await func!.Process(rpc, ctx);
        }
        catch (Exception e)
        {
            _log.Error(e, $"#{rpc.Constructor.ToString("x")} for layer {layer} cannot be processed: {e.Message}");
            var err = RpcErrorGenerator.GenerateError(500, "INTERNAL_SERVER_ERROR"u8);
            return RpcResultGenerator.Generate(err, ctx.MessageId);
        }
    }

    public async ValueTask<TLBytes?> Invoke(ITLStreamingObject rpc, TLExecutionContext ctx,
        int layer = IExecutionEngine.DefaultLayer)
    {
        if (_writeBatches == null || _writeScopeDepth.Value != 0)
        {
            return await InvokeStreamingCore(rpc, ctx, layer);
        }

        _writeScopeDepth.Value++;
        try
        {
            using IWriteBatchScope scope = _writeBatches.BeginScope();
            return await InvokeStreamingCore(rpc, ctx, layer);
        }
        finally
        {
            _writeScopeDepth.Value--;
        }
    }

    private async ValueTask<TLBytes?> InvokeStreamingCore(ITLStreamingObject rpc,
        TLExecutionContext ctx, int layer)
    {
        var authError = await GetAuthError(rpc.Constructor, ctx);
        if (authError != null) return authError;

        try
        {
            var found = _streamingFunctions.TryGetValue(new FunctionKey(layer, rpc.Constructor), out var func);
            if (!found)
            {
                _log.Error($"#{rpc.Constructor.ToString("x")} is not found for layer {layer}");
                var err = RpcErrorGenerator.GenerateError(500, "INTERNAL_SERVER_ERROR"u8);
                return RpcResultGenerator.Generate(err, ctx.MessageId);
            }
            return await func!.Process(rpc, ctx);
        }
        catch (Exception e)
        {
            _log.Error(e, $"#{rpc.Constructor.ToString("x")} for layer {layer} cannot be processed: {e.Message}");
            var err = RpcErrorGenerator.GenerateError(500, "INTERNAL_SERVER_ERROR"u8);
            return RpcResultGenerator.Generate(err, ctx.MessageId);
        }
    }

    public async ValueTask<FileResult> InvokeFile(TLBytes rpc, TLExecutionContext ctx,
        int layer = IExecutionEngine.DefaultLayer)
    {
        if (_writeBatches == null || _writeScopeDepth.Value != 0)
        {
            return await InvokeFileCore(rpc, ctx, layer);
        }

        _writeScopeDepth.Value++;
        try
        {
            using IWriteBatchScope scope = _writeBatches.BeginScope();
            return await InvokeFileCore(rpc, ctx, layer);
        }
        finally
        {
            _writeScopeDepth.Value--;
        }
    }

    private async ValueTask<FileResult> InvokeFileCore(TLBytes rpc, TLExecutionContext ctx,
        int layer)
    {
        if (rpc.Constructor == Constructors.mtproto_GzipPacked)
        {
            using var unpacked = GzipPackedHelper.Unpack(rpc);
            return await InvokeFileCore(unpacked, ctx, layer);
        }

        var authError = await GetAuthError(rpc.Constructor, ctx);
        if (authError != null) return new FileResult(null, authError);

        try
        {
            if (rpc.Constructor == Constructors.baseLayer_InvokeWithLayer)
            {
                using var query = RequestUnwrapper.InvokeWithLayerQuery(rpc, out int requestedLayer);
                return await InvokeFileCore(query, ctx, requestedLayer);
            }
            if (rpc.Constructor == Constructors.baseLayer_InitConnection)
            {
                using var info = InitConnectionFunc.CreateAppInfo(rpc, ctx, _random);
                await _auth.SaveAppInfo(info);
                using var query = RequestUnwrapper.InitConnectionQuery(rpc);
                return await InvokeFileCore(query, ctx, layer);
            }
            if (rpc.Constructor == Constructors.baseLayer_InvokeAfterMsg)
            {
                using var query = RequestUnwrapper.InvokeAfterMsgQuery(rpc);
                return await InvokeFileCore(query, ctx, layer);
            }
            if (rpc.Constructor == Constructors.baseLayer_InvokeAfterMsgs)
            {
                using var query = RequestUnwrapper.InvokeAfterMsgsQuery(rpc);
                return await InvokeFileCore(query, ctx, layer);
            }
            if (rpc.Constructor == Constructors.baseLayer_InvokeWithoutUpdates)
            {
                using var query = RequestUnwrapper.InvokeWithoutUpdatesQuery(rpc);
                return await InvokeFileCore(query, ctx, layer);
            }
            if (rpc.Constructor == Constructors.baseLayer_InvokeWithMessagesRange)
            {
                using var query = RequestUnwrapper.InvokeWithMessagesRangeQuery(rpc);
                return await InvokeFileCore(query, ctx, layer);
            }
            if (rpc.Constructor == Constructors.baseLayer_InvokeWithTakeout)
            {
                using var query = RequestUnwrapper.InvokeWithTakeoutQuery(rpc,
                    out long takeoutId);
                if (!await IsValidTakeoutAsync(takeoutId, ctx.CurrentAuthKeyId))
                {
                    return new FileResult(null, RpcErrorGenerator.GenerateError(
                        400, "TAKEOUT_INVALID"u8));
                }
                return await InvokeFileCore(query, ctx, layer);
            }
            if (rpc.Constructor == Constructors.baseLayer_InvokeWithGooglePlayIntegrityPrefix)
            {
                using var query = RequestUnwrapper.InvokeWithGooglePlayIntegrityQuery(rpc);
                return await InvokeFileCore(query, ctx, layer);
            }
            if (rpc.Constructor == Constructors.baseLayer_InvokeWithApnsSecretPrefix)
            {
                using var query = RequestUnwrapper.InvokeWithApnsSecretQuery(rpc);
                return await InvokeFileCore(query, ctx, layer);
            }
            if (rpc.Constructor == Constructors.baseLayer_InvokeWithReCaptchaPrefix)
            {
                using var query = RequestUnwrapper.InvokeWithReCaptchaQuery(rpc);
                return await InvokeFileCore(query, ctx, layer);
            }
            if (!_fileFunctions.TryGetValue(new FunctionKey(layer, rpc.Constructor), out var func))
            {
                _log.Error($"#{rpc.Constructor.ToString("x")} is not found for layer {layer}");
                return new FileResult(null, RpcErrorGenerator.GenerateError(500, "INTERNAL_SERVER_ERROR"u8));
            }
            return await func!.Process(rpc, ctx);
        }
        catch (Exception e)
        {
            _log.Error(e, $"#{rpc.Constructor.ToString("x")} for layer {layer} cannot be processed: {e.Message}");
            return new FileResult(null, RpcErrorGenerator.GenerateError(500, "INTERNAL_SERVER_ERROR"u8));
        }
    }

    public bool IsFileRequest(TLBytes rpc)
    {
        try
        {
            return IsFileRequestCore(rpc);
        }
        catch
        {
            // Malformed wrappers belong to the normal invocation path, which
            // already owns its error logging and response behavior.
            return false;
        }
    }

    private static bool IsFileRequestCore(TLBytes rpc)
    {
        if (rpc.Constructor == Constructors.baseLayer_GetFile) return true;
        if (rpc.Constructor == Constructors.mtproto_GzipPacked)
        {
            using var unpacked = GzipPackedHelper.Unpack(rpc);
            return IsFileRequestCore(unpacked);
        }
        if (rpc.Constructor == Constructors.baseLayer_InvokeWithLayer)
        {
            using var query = RequestUnwrapper.InvokeWithLayerQuery(rpc, out _);
            return IsFileRequestCore(query);
        }
        if (rpc.Constructor == Constructors.baseLayer_InitConnection)
        {
            using var query = RequestUnwrapper.InitConnectionQuery(rpc);
            return IsFileRequestCore(query);
        }
        if (rpc.Constructor == Constructors.baseLayer_InvokeAfterMsg)
        {
            using var query = RequestUnwrapper.InvokeAfterMsgQuery(rpc);
            return IsFileRequestCore(query);
        }
        if (rpc.Constructor == Constructors.baseLayer_InvokeAfterMsgs)
        {
            using var query = RequestUnwrapper.InvokeAfterMsgsQuery(rpc);
            return IsFileRequestCore(query);
        }
        if (rpc.Constructor == Constructors.baseLayer_InvokeWithoutUpdates)
        {
            using var query = RequestUnwrapper.InvokeWithoutUpdatesQuery(rpc);
            return IsFileRequestCore(query);
        }
        if (rpc.Constructor == Constructors.baseLayer_InvokeWithMessagesRange)
        {
            using var query = RequestUnwrapper.InvokeWithMessagesRangeQuery(rpc);
            return IsFileRequestCore(query);
        }
        if (rpc.Constructor == Constructors.baseLayer_InvokeWithTakeout)
        {
            using var query = RequestUnwrapper.InvokeWithTakeoutQuery(rpc, out _);
            return IsFileRequestCore(query);
        }
        if (rpc.Constructor == Constructors.baseLayer_InvokeWithGooglePlayIntegrityPrefix)
        {
            using var query = RequestUnwrapper.InvokeWithGooglePlayIntegrityQuery(rpc);
            return IsFileRequestCore(query);
        }
        if (rpc.Constructor == Constructors.baseLayer_InvokeWithApnsSecretPrefix)
        {
            using var query = RequestUnwrapper.InvokeWithApnsSecretQuery(rpc);
            return IsFileRequestCore(query);
        }
        if (rpc.Constructor == Constructors.baseLayer_InvokeWithReCaptchaPrefix)
        {
            using var query = RequestUnwrapper.InvokeWithReCaptchaQuery(rpc);
            return IsFileRequestCore(query);
        }
        return false;
    }

    public bool IsImplemented(int constructor, int layer = IExecutionEngine.DefaultLayer)
    {
        try
        {
            var func = _functions[new FunctionKey(layer, constructor)];
            return true;
        }
        catch (Exception e)
        {
            _log.Error(e, $"#{constructor.ToString("x")} is not registered for layer {layer}");
        }

        return false;
    }

    private async ValueTask<bool> IsValidTakeoutAsync(long id, long authKeyId)
    {
        if (_accountSettings is null) return false;
        using TL.baseLayer.dto.TLTakeoutSessionState? session =
            await _accountSettings.GetTakeoutSessionAsync(id);
        return session is not null &&
               session.Value.AsTakeoutSessionState().AuthKeyId == authKeyId &&
               session.Value.AsTakeoutSessionState().ExpiresAt >
               _time.GetUtcNow().ToUnixTimeSeconds();
    }

    private async ValueTask<TLBytes?> GetAuthError(int constructor, TLExecutionContext ctx)
    {
        var keyStatus = await _mtproto.GetKeyStatus(ctx.CurrentAuthKeyId);
        if (ctx.CurrentAuthKeyId != 0 &&
            keyStatus == KeyStatus.TempUnbound &&
            !IsTempKeyAllowed(constructor))
        {
            return RpcError.Builder()
                .ErrorCode(401)
                .ErrorMessage("AUTH_KEY_PERM_EMPTY"u8)
                .Build().TLBytes;
        }
        if (RequiresAuthorization(constructor) &&
            !await _auth.IsAuthorized(ctx.CurrentAuthKeyId))
        {
            return RpcError.Builder()
                .ErrorCode(401)
                .ErrorMessage("AUTH_KEY_UNREGISTERED"u8)
                .Build().TLBytes;
        }

        return null;
    }

    private bool RequiresAuthorization(int constructor)
    {
        return !AuthPolicy.UnauthorizedMethods.Contains(constructor);
    }
    
    private bool IsTempKeyAllowed(int constructor)
    {
        return AuthPolicy.TempKeyAllowedMethods.Contains(constructor);
    }
}
