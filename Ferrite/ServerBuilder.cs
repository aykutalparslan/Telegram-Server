// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Numerics;
using System.Reflection;
using Autofac;
using Autofac.Core;
using Ferrite.Core;
using Ferrite.Core.Calls;
using Ferrite.Core.Connection;
using Ferrite.Core.Connection.TransportFeatures;
using Ferrite.Core.Execution;
using Ferrite.Core.Execution.Functions;
using Ferrite.Core.Execution.Functions.BaseLayer;
using Ferrite.Core.Framing;
using Ferrite.Core.RequestChain;
using Ferrite.Crypto;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.GroupCallMedia;
using Ferrite.Services;
using Ferrite.Services.Calls;
using Ferrite.Services.Calls.E2E;
using Ferrite.Services.Gateway;
using Ferrite.Services.Phone.Handlers;
using Ferrite.Services.SecretChats;
using Ferrite.Services.SecretChats.Handlers;
using Ferrite.Services.Stats;
using Ferrite.TL;
using Ferrite.Transport;
using Ferrite.Utils;

namespace Ferrite;

public class ServerBuilder
{
    public static IFerriteServer BuildServer(string ipAddress, int port, string path = "data")
    {
        return BuildServer(new FerriteServerOptions
        {
            PublicAddress = ipAddress,
            Port = port,
            DataPath = path
        });
    }

    public static IFerriteServer BuildServer(FerriteServerOptions options)
    {
        IContainer container = BuildContainer(options);
        var keyProvider = container.Resolve<IKeyProvider>();
        var fingerprints = keyProvider.GetRSAFingerprints();
        foreach (var fingerprint in fingerprints)
        {
            var key = keyProvider.GetKey(fingerprint);
            Console.WriteLine(key?.ExportPublicKey());
            Console.WriteLine($"Modulus: {new BigInteger(key?.PublicKeyParameters.Modulus, true, true)}");
            Console.WriteLine($"Exponent: {new BigInteger(key?.PublicKeyParameters.Exponent,true,true)}");
            Console.WriteLine($"Fingerprint-HEX: 0x{fingerprint:X}");
            Console.WriteLine($"Fingerprint-DECIMAL: {fingerprint}");
        }
        
        return container.Resolve<IFerriteServer>();
    }
    internal static IContainer BuildContainer(FerriteServerOptions options)
    {
        var builder = new ContainerBuilder();
        RegisterPrimitives(builder);
        RegisterServices(builder, options);
        RegisterCoreComponents(builder);
        if (!options.Storage.TryValidate(out string storageError))
        {
            throw new ArgumentException(storageError, nameof(options));
        }
        RegisterDataStores(builder, options.DataPath, options.Storage);
        builder.Register(_ => new DataCenter(1, options.PublicAddress,
                options.Port, false))
            .As<IDataCenter>().SingleInstance();
        // Bind and advertised call-media endpoints stay separate from the
        // MTProto DataCenter address. The development default binds an
        // ephemeral port; only an empty advertised address falls back to the
        // server's public address.
        CallMediaRelayOptions callMedia = options.CallMedia
            ?? new CallMediaRelayOptions();
        if (callMedia.AdvertisedAddress.Length == 0)
        {
            callMedia = callMedia with { AdvertisedAddress = options.PublicAddress };
        }
        if (!callMedia.TryValidate(out string callMediaError))
        {
            throw new ArgumentException(callMediaError, nameof(options));
        }
        CallTurnOptions callTurn = options.CallTurn ?? new CallTurnOptions();
        if (!callTurn.TryValidate(out string callTurnError))
        {
            throw new ArgumentException(callTurnError, nameof(options));
        }
        builder.RegisterInstance(callMedia).SingleInstance();
        builder.RegisterInstance(callTurn).SingleInstance();
        builder.RegisterType<SerilogLogger>().As<ILogger>().SingleInstance();
        var container = builder.Build();
        return container;
    }

    private static void RegisterPrimitives(ContainerBuilder builder)
    {
        builder.RegisterType<MTProtoTime>().As<IMTProtoTime>().SingleInstance();
        builder.RegisterType<RandomGenerator>().As<IRandomGenerator>().SingleInstance();
        builder.RegisterType<KeyProvider>().As<IKeyProvider>().SingleInstance();
    }

    private static void RegisterDataStores(ContainerBuilder builder, string path,
        StorageOptions options)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        ResolvedStorageOptions selected = options.Resolve();
        if (selected.KeyValue == KeyValueBackend.RocksDb)
        {
            builder.Register(_ => new RocksDbKVStoreFactory(
                    Path.Combine(path, "rocksdb-data")))
                .As<IKVStoreFactory>().SingleInstance();
            builder.RegisterType<ImmediateWriteBatchAccessor>()
                .As<IWriteBatchAccessor>().SingleInstance();
        }
        else
        {
            builder.Register(_ => new CassandraKVStoreFactory(
                    options.CassandraKeyspace, options.CassandraPort,
                    options.CassandraHosts))
                .As<IKVStoreFactory>().SingleInstance();
            builder.Register(c =>
                {
                    var factory = (CassandraKVStoreFactory)c.Resolve<IKVStoreFactory>();
                    return new CassandraWriteBatchAccessor(factory.Context,
                        options.CassandraKeyspace);
                })
                .As<IWriteBatchAccessor>().SingleInstance();
        }

        if (selected.Ephemeral == EphemeralBackend.InMemory)
        {
            builder.RegisterType<InMemoryStoreFactory>()
                .As<IVolatileKVStoreFactory>().SingleInstance();
        }
        else
        {
            builder.Register(_ => new RedisDataStoreFactory(options.RedisConfiguration))
                .As<IVolatileKVStoreFactory>().SingleInstance();
        }

        switch (selected.Pipe)
        {
            case PipeBackend.Local:
                builder.RegisterType<LocalPipe>().As<IMessagePipe>().SingleInstance();
                break;
            case PipeBackend.Redis:
                builder.Register(_ => new RedisPipe(options.RedisConfiguration))
                    .As<IMessagePipe>().SingleInstance();
                break;
            case PipeBackend.Kafka:
                builder.Register(_ => new KafkaPipe(options.KafkaConfiguration))
                    .As<IMessagePipe>().SingleInstance();
                break;
        }

        if (selected.ObjectStore == ObjectStoreBackend.Local)
        {
            builder.Register(_ => new LocalObjectStore(
                    Path.Combine(path, "uploaded-files")))
                .As<IObjectStore>().SingleInstance();
        }
        else
        {
            builder.Register(_ => new S3ObjectStore(options.S3ServiceUrl,
                    options.S3AccessKey, options.S3SecretKey))
                .As<IObjectStore>().SingleInstance();
        }

        if (selected.Search == SearchBackend.Lucene)
        {
            builder.Register(_ => new LuceneSearchEngine(
                    Path.Combine(path, "lucene-index-data")))
                .As<ISearchEngine>().SingleInstance();
        }
        else
        {
            builder.Register(_ => new ElasticSearchEngine(options.ElasticsearchUrl,
                    options.ElasticsearchUsername, options.ElasticsearchPassword,
                    options.ElasticsearchFingerprint))
                .As<ISearchEngine>().SingleInstance();
        }

        if (selected.Counters == CounterBackend.Faster)
        {
            builder.Register(_ => new FasterCounterFactory(
                    Path.Combine(path, "faster-counter-data")))
                .As<ICounterFactory>().SingleInstance();
        }
        else
        {
            builder.Register(_ => new RedisCounterFactory(options.RedisConfiguration))
                .As<ICounterFactory>().SingleInstance();
        }
        builder.RegisterType<ChatIdAllocator>()
            .As<IChatIdAllocator>().SingleInstance();

        if (selected.UpdatesContext == UpdatesContextBackend.Faster)
        {
            builder.Register(_ => new FasterUpdatesContextFactory(
                    Path.Combine(path, "faster-updates-data")))
                .As<IUpdatesContextFactory>().SingleInstance();
        }
        else
        {
            builder.Register(_ => new RedisUpdatesContextFactory(
                    options.RedisConfiguration))
                .As<IUpdatesContextFactory>().SingleInstance();
        }

        builder.RegisterType<StorageUnitOfWork>()
            .As<IUnitOfWork>().SingleInstance();
        RegisterRepositories(builder);
    }

    private static void RegisterRepositories(ContainerBuilder builder)
    {
        static IKVStore Durable(IComponentContext context) =>
            context.Resolve<IKVStoreFactory>()
                .Create(context.Resolve<IWriteBatchAccessor>());
        static IVolatileKVStore Ephemeral(IComponentContext context) =>
            context.Resolve<IVolatileKVStoreFactory>().Create();

        builder.Register(c => new AuthKeyRepository(Durable(c), Ephemeral(c)))
            .As<IAuthKeyRepository>().SingleInstance();
        builder.Register(c => new AuthorizationRepository(Durable(c), Durable(c)))
            .As<IAuthorizationRepository>().SingleInstance();
        builder.Register(c => new TempAuthKeyRepository(Ephemeral(c)))
            .As<ITempAuthKeyRepository>().SingleInstance();
        builder.Register(c => new BoundAuthKeyRepository(Ephemeral(c), Ephemeral(c),
                Ephemeral(c)))
            .As<IBoundAuthKeyRepository>().SingleInstance();
        builder.Register(c => new UpdatesStateRepository(Durable(c)))
            .As<IUpdatesStateRepository>().SingleInstance();
        builder.Register(c => new MessageRepository(Durable(c),
                c.Resolve<IUpdatesStateRepository>()))
            .As<IMessageRepository>().SingleInstance();
        builder.Register(c => new DraftsRepository(Durable(c)))
            .As<IDraftsRepository>().SingleInstance();
        builder.Register(c => new WebPagesRepository(Durable(c)))
            .As<IWebPagesRepository>().SingleInstance();
        builder.Register(c => new ChannelContentReadsRepository(Durable(c)))
            .As<IChannelContentReadsRepository>().SingleInstance();
        builder.Register(c => new MessageInteractionsRepository(Durable(c), Durable(c)))
            .As<IMessageInteractionsRepository>().SingleInstance();
        builder.Register(c => new MessageReadReceiptsRepository(Durable(c)))
            .As<IMessageReadReceiptsRepository>().SingleInstance();
        builder.Register(c => new PollsRepository(Durable(c), Durable(c)))
            .As<IPollsRepository>().SingleInstance();
        builder.Register(c => new ScheduledMessagesRepository(Durable(c)))
            .As<IScheduledMessagesRepository>().SingleInstance();
        builder.Register(c => new MessagingSettingsRepository(Durable(c), Durable(c),
                Durable(c), Durable(c)))
            .As<IMessagingSettingsRepository>().SingleInstance();
        builder.Register(c => new ExpiringMessagesRepository(Durable(c)))
            .As<IExpiringMessagesRepository>().SingleInstance();
        builder.Register(c => new TopPeersRepository(Durable(c)))
            .As<ITopPeersRepository>().SingleInstance();
        builder.Register(c => new DialogOrganizationRepository(Durable(c), Durable(c),
                Durable(c), Durable(c), Durable(c)))
            .As<IDialogOrganizationRepository>().SingleInstance();
        builder.Register(c => new StickerRepository(Durable(c), Durable(c), Durable(c),
                Durable(c), Durable(c)))
            .As<IStickerRepository>().SingleInstance();
        builder.Register(c => new ChannelAdminRepository(Durable(c), Durable(c)))
            .As<IChannelAdminRepository>().SingleInstance();
        builder.Register(c => new ChannelAdminLogRepository(Durable(c)))
            .As<IChannelAdminLogRepository>().SingleInstance();
        builder.Register(c => new StatisticsRepository(Durable(c), Durable(c)))
            .As<IStatisticsRepository>().SingleInstance();
        builder.Register(c => new AccountSettingsRepository(Durable(c), Durable(c),
                Durable(c), Durable(c), Durable(c), Durable(c), Durable(c), Durable(c),
                Durable(c), Durable(c), Durable(c), Durable(c), Durable(c)))
            .As<IAccountSettingsRepository>().SingleInstance();
        builder.Register(c => new NearbyLocationsRepository(Durable(c)))
            .As<INearbyLocationsRepository>().SingleInstance();
        builder.Register(c => new ModerationRepository(Durable(c), Durable(c)))
            .As<IModerationRepository>().SingleInstance();
        builder.Register(c => new UserStatusRepository(Durable(c)))
            .As<IUserStatusRepository>().SingleInstance();
        builder.Register(c => new SessionRepository(Ephemeral(c), Ephemeral(c)))
            .As<ISessionRepository>().SingleInstance();
        builder.Register(c => new AuthSessionRepository(Ephemeral(c)))
            .As<IAuthSessionRepository>().SingleInstance();
        builder.Register(c => new PhoneCodeRepository(Ephemeral(c)))
            .As<IPhoneCodeRepository>().SingleInstance();
        builder.Register(c => new SignInRepository(Ephemeral(c)))
            .As<ISignInRepository>().SingleInstance();
        builder.Register(c => new ServerSaltRepository(Ephemeral(c), Ephemeral(c)))
            .As<IServerSaltRepository>().SingleInstance();
        builder.Register(c => new DeviceLockedRepository(Ephemeral(c)))
            .As<IDeviceLockedRepository>().SingleInstance();
        builder.Register(c => new UserRepository(Durable(c), Durable(c), Durable(c)))
            .As<IUserRepository>().SingleInstance();
        builder.Register(c => new AppInfoRepository(Durable(c)))
            .As<IAppInfoRepository>().SingleInstance();
        builder.Register(c => new DeviceInfoRepository(Durable(c), Durable(c)))
            .As<IDeviceInfoRepository>().SingleInstance();
        builder.Register(c => new NotifySettingsRepository(Durable(c)))
            .As<INotifySettingsRepository>().SingleInstance();
        builder.Register(c => new ReportReasonRepository(Durable(c)))
            .As<IReportReasonRepository>().SingleInstance();
        builder.Register(c => new PrivacyRulesRepository(Durable(c)))
            .As<IPrivacyRulesRepository>().SingleInstance();
        builder.Register(c => new ChatRepository(Durable(c), Durable(c), Durable(c)))
            .As<IChatRepository>().SingleInstance();
        builder.Register(c => new ChatParticipantsRepository(Durable(c)))
            .As<IChatParticipantsRepository>().SingleInstance();
        builder.Register(c => new ChatInvitesRepository(Durable(c), Durable(c), Durable(c)))
            .As<IChatInvitesRepository>().SingleInstance();
        builder.Register(c => new ForumTopicsRepository(Durable(c), Durable(c), Durable(c)))
            .As<IForumTopicsRepository>().SingleInstance();
        builder.Register(c => new ChannelMessagesRepository(Durable(c), Durable(c),
                Durable(c)))
            .As<IChannelMessagesRepository>().SingleInstance();
        builder.Register(c => new MessageReactionsRepository(Durable(c), Durable(c),
                Durable(c)))
            .As<IMessageReactionsRepository>().SingleInstance();
        builder.Register(c => new ContactsRepository(Durable(c), Durable(c)))
            .As<IContactsRepository>().SingleInstance();
        builder.Register(c => new BlockedPeersRepository(Durable(c)))
            .As<IBlockedPeersRepository>().SingleInstance();
        builder.Register(c => new FileInfoRepository(Durable(c), Durable(c), Durable(c),
                Durable(c), Durable(c), Durable(c)))
            .As<IFileInfoRepository>().SingleInstance();
        builder.Register(c => new PhotoRepository(Durable(c), Durable(c), Durable(c)))
            .As<IPhotoRepository>().SingleInstance();
        builder.Register(c => new DocumentsRepository(Durable(c), Durable(c)))
            .As<IDocumentsRepository>().SingleInstance();
        builder.Register(c => new LangPackRepository(Durable(c), Durable(c)))
            .As<ILangPackRepository>().SingleInstance();
        builder.Register(c => new SignUpNotificationRepository(Durable(c)))
            .As<ISignUpNotificationRepository>().SingleInstance();
        builder.Register(c => new SecretChatsRepository(Durable(c), Durable(c), Durable(c),
                Durable(c), Durable(c), Durable(c), Durable(c), Durable(c), Durable(c),
                Durable(c), Durable(c), c.Resolve<IUnitOfWork>().SaveAsync))
            .As<ISecretChatsRepository>().SingleInstance();
        builder.Register(c => new GroupCallsRepository(Durable(c), Durable(c), Durable(c),
                Durable(c), Durable(c), Durable(c), Durable(c),
                c.Resolve<IUnitOfWork>().SaveAsync))
            .As<IGroupCallsRepository>().SingleInstance();
        builder.Register(c => new GroupCallChainRepository(Durable(c), Durable(c),
                c.Resolve<IUnitOfWork>().SaveAsync))
            .As<IGroupCallChainRepository>().SingleInstance();
        builder.Register(c => new AccountPasswordRepository(Durable(c), Durable(c),
                Ephemeral(c), Ephemeral(c), c.Resolve<IUnitOfWork>().SaveAsync))
            .As<IAccountPasswordRepository>().SingleInstance();
        builder.Register(c => new VerificationCodeRepository(Ephemeral(c), Ephemeral(c),
                Ephemeral(c)))
            .As<IVerificationCodeRepository>().SingleInstance();
        builder.Register(c => new LoginAttemptRepository(Ephemeral(c), Ephemeral(c)))
            .As<ILoginAttemptRepository>().SingleInstance();
        builder.Register(c => new LoginTokenRepository(Ephemeral(c), Ephemeral(c)))
            .As<ILoginTokenRepository>().SingleInstance();
    }

    private static void RegisterCoreComponents(ContainerBuilder builder)
    {
        builder.RegisterType<MTProtoConnection>();
        builder.RegisterType<AuthKeyProcessor>();
        builder.RegisterType<MsgContainerProcessor>();
        builder.RegisterType<ServiceMessagesProcessor>();
        builder.RegisterType<GZipProcessor>();
        builder.RegisterType<MTProtoRequestProcessor>();
        builder.RegisterType<DefaultChain>().As<ITLHandler>().SingleInstance();
        RegisterApiLayers(builder);
        builder.RegisterType<ExecutionEngine>().As<IExecutionEngine>().SingleInstance();
        builder.RegisterType<ProtoHandler>().As<IProtoHandler>();
        builder.RegisterType<QuickAckFeature>().As<IQuickAckFeature>().SingleInstance();
        builder.RegisterType<TransportErrorFeature>().As<ITransportErrorFeature>().SingleInstance();
        builder.RegisterType<WebSocketFeature>().As<IWebSocketFeature>();
        builder.RegisterType<ProtoTransport>();
        builder.RegisterType<MTProtoSession>().As<IMTProtoSession>();
        builder.RegisterType<MTProtoTransportDetector>().As<ITransportDetector>();
        builder.RegisterType<SocketConnectionListener>().As<IConnectionListener>();
        builder.RegisterType<FerriteServer>().As<IFerriteServer>().SingleInstance();
    }

    internal static void RegisterApiLayers(ContainerBuilder builder)
    {
        // Bespoke protocol handlers self-declare their FunctionKey at class level.
        // Business handlers in Ferrite.Services declare it on Handle; the generic
        // adapter below supplies CurrentAuthKeyId and the rpc_result envelope so
        // method orchestration never moves into Ferrite.Core.
        Type[] handlerTypes = typeof(ITLFunction).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && t.GetCustomAttribute<TLFunctionAttribute>() is not null)
            .ToArray();
        var serviceMethods = typeof(DialogBuilder).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public |
                                          BindingFlags.DeclaredOnly))
            .Select(m => (Method: m, Attribute: m.GetCustomAttribute<TLFunctionAttribute>()))
            .Where(x => x.Attribute is not null)
            .Select(x => (x.Method, Attribute: x.Attribute!))
            .ToArray();

        EnsureUniqueDispatchKeys(handlerTypes, serviceMethods);

        foreach (Type serviceHandlerType in serviceMethods
                     .Select(x => x.Method.DeclaringType!)
                     .Where(t => t.IsClass)
                     .Distinct())
        {
            builder.RegisterType(serviceHandlerType).AsSelf().SingleInstance();
        }

        builder.RegisterAssemblyTypes(typeof(ITLFunction).Assembly)
            .Where(t => t.GetCustomAttribute<TLFunctionAttribute>() is not null)
            .As(t =>
            {
                var a = t.GetCustomAttribute<TLFunctionAttribute>()!;
                var iface = typeof(ITLStreamingFunction).IsAssignableFrom(t)
                    ? typeof(ITLStreamingFunction)
                    : typeof(ITLFileFunction).IsAssignableFrom(t)
                        ? typeof(ITLFileFunction)
                        : typeof(ITLFunction);
                return new[] { new KeyedService(new FunctionKey(a.Layer, a.Constructor), iface) };
            })
            .SingleInstance()
            .PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);

        foreach (var (method, attribute) in serviceMethods)
        {
            Func<object, ITLFunction> functionFactory =
                ServiceMethodFunction.CreateFactory(method);
            builder.Register(c => functionFactory(c.Resolve(method.DeclaringType!)))
                .Keyed<ITLFunction>(new FunctionKey(attribute.Layer, attribute.Constructor))
                .SingleInstance();
        }

        builder.RegisterType<DisabledFunc>()
            .As(DisabledMethods.Keys.Select(k => new KeyedService(k, typeof(ITLFunction))).ToArray())
            .SingleInstance();

        // The deferred bucket is empty, and Autofac refuses a
        // registration that exposes no service at all, so the 501 fallback is
        // only registered while something still needs it.
        if (NotImplementedMethods.Keys.Length > 0)
        {
            builder.RegisterType<NotImplementedFunc>()
                .As(NotImplementedMethods.Keys.Select(k => new KeyedService(k, typeof(ITLFunction))).ToArray())
                .SingleInstance();
        }
    }

    internal static void EnsureUniqueDispatchKeys(Type[] handlerTypes,
        (MethodInfo Method, TLFunctionAttribute Attribute)[] serviceMethods)
    {
        var declarations = handlerTypes.Select(t =>
            {
                var attribute = t.GetCustomAttribute<TLFunctionAttribute>()!;
                Type functionType = typeof(ITLStreamingFunction).IsAssignableFrom(t)
                    ? typeof(ITLStreamingFunction)
                    : typeof(ITLFileFunction).IsAssignableFrom(t)
                        ? typeof(ITLFileFunction)
                        : typeof(ITLFunction);
                return (Key: new FunctionKey(attribute.Layer, attribute.Constructor),
                    FunctionType: functionType, Source: t.FullName!);
            })
            .Concat(serviceMethods.Select(x =>
                (Key: new FunctionKey(x.Attribute.Layer, x.Attribute.Constructor),
                    FunctionType: typeof(ITLFunction),
                    Source: $"{x.Method.DeclaringType!.FullName}.{x.Method.Name}")))
            .Concat(DisabledMethods.Keys.Select(k =>
                (Key: k, FunctionType: typeof(ITLFunction),
                    Source: $"{typeof(DisabledMethods).FullName}")))
            .Concat(NotImplementedMethods.Keys.Select(k =>
                (Key: k, FunctionType: typeof(ITLFunction),
                    Source: $"{typeof(NotImplementedMethods).FullName}")))
            .ToArray();

        var duplicate = declarations
            .GroupBy(x => (x.Key, x.FunctionType))
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
        {
            throw new InvalidOperationException(
                $"Duplicate TL dispatch key declared by {string.Join(", ", duplicate.Select(x => x.Source))}.");
        }
    }

    private static void RegisterServices(ContainerBuilder builder,
        FerriteServerOptions options)
    {
        builder.RegisterType<VerificationGateway>()
            .As<IVerificationGateway>().SingleInstance();
        builder.RegisterType<RejectingWebAuthorizationTokenValidator>()
            .As<IWebAuthorizationTokenValidator>().SingleInstance();
        builder.RegisterType<RejectingDeviceAttestationTokenValidator>()
            .As<IDeviceAttestationTokenValidator>().SingleInstance();
        builder.RegisterType<RejectingEmailIdentityTokenValidator>()
            .As<IEmailIdentityTokenValidator>().SingleInstance();
        builder.RegisterType<AuthorizationCompletion>()
            .As<IAuthorizationCompletion>().SingleInstance();
        builder.RegisterType<VerificationCodeService>()
            .As<IVerificationCodeService>().SingleInstance();
        builder.RegisterType<AccountPasswordManager>()
            .As<IAccountPasswordManager>().SingleInstance();
        builder.RegisterType<PasswordResetService>()
            .As<IPasswordResetService>().SingleInstance();
        builder.RegisterType<PasswordRecoveryService>()
            .As<IPasswordRecoveryService>().SingleInstance();
        builder.RegisterType<LoginTokenService>()
            .As<ILoginTokenService>().SingleInstance();
        builder.RegisterType<MTProtoService>().As<IMTProtoService>()
            .SingleInstance();
        builder.RegisterType<UpdatesService>().As<IUpdatesService>()
            .SingleInstance();
        builder.RegisterType<UpdatesStateService>().As<IUpdatesStateService>()
            .SingleInstance();
        builder.RegisterType<SecretChatDeviceSelector>()
            .As<ISecretChatDeviceSelector>().SingleInstance();
        builder.RegisterInstance(new SecretChatLimits()).SingleInstance();
        builder.RegisterType<SecretChatTelemetry>().AsSelf().SingleInstance();
        builder.RegisterType<SecretChatMaintenance>()
            .As<ISecretChatMaintenance>().SingleInstance();
        builder.RegisterType<SecretChatQtsQueue>()
            .As<ISecretChatQtsQueue>().SingleInstance();
        builder.RegisterType<SecretChatEncryptedFileResolver>().AsSelf()
            .SingleInstance();
        builder.RegisterType<SecretChatControlDelivery>().AsSelf().SingleInstance();
        builder.RegisterType<SecretChatTransitionRepair>()
            .As<ISecretChatTransitionRepair>().SingleInstance();
        builder.RegisterType<SecretChatAuthKeyCleanup>()
            .As<ISecretChatAuthKeyCleanup>().SingleInstance();
        // 1:1 call collaborators. CallMediaRelayOptions and
        // CallTurnOptions instances come from the structured server options in
        // BuildContainer; no PhoneService aggregate or manual function key is
        // registered — phone.* handlers register through the assembly scan.
        builder.RegisterInstance(TimeProvider.System).As<TimeProvider>()
            .SingleInstance();
        builder.RegisterInstance(new CallRegistryOptions()).SingleInstance();
        builder.RegisterType<CallRegistry>().As<ICallRegistry>().SingleInstance();
        builder.RegisterType<TelegramCallReflector>().As<ICallMediaRelay>()
            .SingleInstance();
        builder.RegisterType<CoturnRestCredentialProvider>()
            .As<ITurnCredentialProvider>().SingleInstance();
        builder.RegisterInstance(new StaticTurnEndpointHealth(true))
            .As<ITurnEndpointHealth>().SingleInstance();
        builder.RegisterType<CallTurnConnectionBuilder>().AsSelf().SingleInstance();
        builder.RegisterInstance(new CallSignalingLimiterOptions()).SingleInstance();
        builder.RegisterType<CallSignalingLimiter>().AsSelf().SingleInstance();
        builder.RegisterType<CallTerminator>().AsSelf().SingleInstance();
        builder.RegisterType<PhotoProcessingService>().As<IPhotoProcessingService>()
            .SingleInstance();
        builder.RegisterType<UploadService>().As<IUploadService>()
            .SingleInstance();
        builder.RegisterType<ChatRowStore>().AsSelf().SingleInstance();
        builder.RegisterType<GroupCallChatLink>().AsSelf().SingleInstance();
        builder.RegisterType<GroupCallActionMessages>().AsSelf().SingleInstance();
        builder.RegisterInstance(new GroupCallActivityOptions()).AsSelf().SingleInstance();
        GroupCallVideoOptions groupCallVideo = options.GroupCallVideo ??
            new GroupCallVideoOptions();
        groupCallVideo.Validate();
        builder.RegisterInstance(groupCallVideo)
            .AsSelf().SingleInstance();
        builder.RegisterInstance(options.GroupCallMediaRuntime ??
                new GroupCallMediaRuntimeOptions())
            .AsSelf().SingleInstance();
        GroupCallBroadcastOptions groupCallBroadcast = options.GroupCallBroadcast ??
            new GroupCallBroadcastOptions();
        groupCallBroadcast.Validate();
        builder.RegisterInstance(groupCallBroadcast).AsSelf().SingleInstance();
        GroupCallRecordingOptions groupCallRecording = options.GroupCallRecording ??
            new GroupCallRecordingOptions();
        groupCallRecording.Validate();
        builder.RegisterInstance(groupCallRecording).AsSelf().SingleInstance();
        builder.RegisterType<GroupCallActivityTracker>().AsSelf().SingleInstance();
        // Live per-viewer SSRC mappings. Deliberately in-memory: the worker
        // re-derives them on every join, so a persisted snapshot would outlive the
        // transports it names.
        builder.RegisterType<GroupCallMediaSourceMap>().AsSelf().SingleInstance();
        // The tde2e conference chain: the authoritative validator and ordering
        // server for E2E conference calls, plus the join half both
        // createConferenceCall and joinGroupCall's flags.3 branch run.
        builder.RegisterType<GroupCallChainService>()
            .As<IGroupCallChainService>().AsSelf().SingleInstance();
        builder.RegisterType<ConferenceJoinOperation>().AsSelf().SingleInstance();
        builder.RegisterInstance(new GroupCallDisconnectOptions()).AsSelf()
            .SingleInstance();
        builder.RegisterType<GroupCallDisconnectMonitor>().AsSelf().SingleInstance();
        builder.RegisterType<GroupCallSourcesChangedMonitor>().AsSelf().SingleInstance();
        if (options.GroupCallMediaWorker is { } workerOptions)
        {
            workerOptions.Validate();
            builder.RegisterInstance(workerOptions).AsSelf().SingleInstance();
            builder.Register(_ => new HttpClient()).AsSelf().SingleInstance();
            builder.RegisterType<MediasoupGroupCallMediaPlane>()
                .As<IGroupCallMediaPlane>().AsSelf().SingleInstance();
            builder.RegisterType<MediasoupGroupCallBroadcastPlane>()
                .As<IGroupCallBroadcastPlane>().AsSelf().SingleInstance();
            builder.RegisterType<MediasoupGroupCallRecorder>()
                .As<IGroupCallRecorder>().AsSelf().SingleInstance();
        }
        else
        {
            builder.RegisterType<UnavailableGroupCallMediaPlane>()
                .As<IGroupCallMediaPlane>().SingleInstance();
            builder.RegisterType<UnavailableGroupCallBroadcastPlane>()
                .As<IGroupCallBroadcastPlane>().SingleInstance();
            builder.RegisterType<UnavailableGroupCallRecorder>()
                .As<IGroupCallRecorder>().SingleInstance();
        }
        builder.RegisterType<GroupCallMediaRuntime>().AsSelf().SingleInstance();
        builder.RegisterType<GroupCallBroadcastRuntime>().AsSelf().SingleInstance();
        builder.RegisterType<GroupCallRecordingDelivery>()
            .As<IGroupCallRecordingDelivery>().AsSelf().SingleInstance();
        builder.RegisterType<GroupCallRecordingCoordinator>()
            .As<IGroupCallRecordingCoordinator>().AsSelf().SingleInstance();
        builder.RegisterType<GroupCallRecordingRuntime>().AsSelf().SingleInstance();
        builder.RegisterType<InviteStore>().AsSelf().SingleInstance();
        builder.RegisterType<ReactionStore>().AsSelf().SingleInstance();
        builder.RegisterType<PrivacyEvaluator>().AsSelf().SingleInstance();
        builder.RegisterType<IdAllocators>().AsSelf().SingleInstance();
        builder.RegisterType<MessageStore>().AsSelf().SingleInstance();
        builder.RegisterType<MessageLocator>().AsSelf().SingleInstance();
        builder.RegisterType<ReadReceiptStore>().AsSelf().SingleInstance();
        builder.RegisterType<MentionScope>().AsSelf().SingleInstance();
        builder.RegisterType<DraftStore>().AsSelf().SingleInstance();
        builder.RegisterType<PollStore>().AsSelf().SingleInstance();
        builder.RegisterType<ChatSettingsStore>().AsSelf().SingleInstance();
        builder.RegisterType<ModerationStore>().AsSelf().SingleInstance();
        builder.RegisterType<NearbyLocationStore>().AsSelf().SingleInstance();
        builder.RegisterType<MessageExpiryStore>().AsSelf().SingleInstance();
        builder.RegisterType<MessageExpiryRuntime>().AsSelf().SingleInstance();
        builder.RegisterType<ScheduledMessageStore>().AsSelf().SingleInstance();
        builder.RegisterType<ScheduledMessageSender>().AsSelf().SingleInstance();
        builder.RegisterType<ScheduledMessageFlusher>().AsSelf().SingleInstance();
        builder.RegisterType<ScheduledMessageRuntime>().AsSelf().SingleInstance();
        builder.RegisterType<UpdateFanout>().AsSelf().SingleInstance();
        builder.RegisterType<DialogOrganizationStore>().AsSelf().SingleInstance();
        builder.RegisterType<DialogFilterStore>().AsSelf().SingleInstance();
        builder.RegisterType<ChatlistInviteStore>().AsSelf().SingleInstance();
        builder.RegisterType<StickerStore>().AsSelf().SingleInstance();
        builder.RegisterType<StatisticsStore>().AsSelf().SingleInstance();
        builder.RegisterType<StatsGraphTokens>().AsSelf().SingleInstance();
        builder.RegisterType<AccountSettingsStore>().AsSelf().SingleInstance();
        builder.RegisterType<ProfileStore>().AsSelf().SingleInstance();
        builder.RegisterType<WallpaperStore>().AsSelf().SingleInstance();
        builder.RegisterType<ThemeStore>().AsSelf().SingleInstance();
        builder.RegisterType<AccountAudioStore>().AsSelf().SingleInstance();
        builder.RegisterType<DialogBuilder>().AsSelf().SingleInstance();
        builder.RegisterType<MessageSearchService>().AsSelf().SingleInstance();
        builder.RegisterType<PublicPostSearchService>().AsSelf().SingleInstance();
        builder.RegisterType<SendPipeline>().AsSelf().SingleInstance();
        builder.RegisterType<MediaMessageSender>().AsSelf().SingleInstance();
        builder.Register(c => new SessionService(c.Resolve<IUnitOfWork>(),
                c.Resolve<IAuthSessionRepository>(),
                c.Resolve<ISessionRepository>(), c.Resolve<ILogger>(),
                options.NodeId))
            .As<ISessionService>().SingleInstance();
        builder.RegisterType<AuthService>().As<IAuthService>().SingleInstance();
        builder.RegisterType<SkiaPhotoProcessor>().As<IPhotoProcessor>()
            .SingleInstance();
    }
}
