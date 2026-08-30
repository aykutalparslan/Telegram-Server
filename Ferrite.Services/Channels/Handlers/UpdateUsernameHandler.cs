// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using System.Text.RegularExpressions;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class UpdateUsernameHandler : ChannelsHandlerBase
{
    private readonly IChatRepository _chatRepository;

    public UpdateUsernameHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ICounterFactory counterFactory,
        IdAllocators ids, IUpdatesContextFactory updatesContextFactory,
        IUpdatesService updates, ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _chatRepository = chatRepository;

    }

    [TLFunction(Constructors.baseLayer_ChannelsUpdateUsername)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = (ChannelsUpdateUsername)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        string username = Encoding.UTF8.GetString(request.Username);

        var (currentUserId, channelBytes, error) =
            await PrepareChannelMutationCore(authKeyId, channelId, creatorOnly: true);
        if (error != null)
        {
            return ErrorBool(Encoding.UTF8.GetBytes(error));
        }

        string oldUsername = ReadChannelUsername(channelBytes);
        if (username == oldUsername)
        {
            return ErrorBool("USERNAME_NOT_MODIFIED"u8);
        }
        if (username.Length > 0 && !UsernameRegex.IsMatch(username))
        {
            return ErrorBool("USERNAME_INVALID"u8);
        }
        if (username.Length > 0 && IsUsernameOccupied(username, channelId))
        {
            return ErrorBool("USERNAME_OCCUPIED"u8);
        }

        long id = channelId!.Value;
        if (oldUsername.Length > 0)
        {
            _chatRepository.DeleteUsername(oldUsername);
        }
        if (username.Length > 0)
        {
            _chatRepository.PutUsername(username, id);
        }
        byte[] updatedChannelBytes = RebuildChannelRowWithUsername(channelBytes, username);

        byte[] logAction;
        using (TLChannelAdminLogEventAction action =
               ChannelAdminLogEventActionChangeUsername.Builder()
                   .PrevValue(Encoding.UTF8.GetBytes(oldUsername))
                   .NewValue(Encoding.UTF8.GetBytes(username))
                   .Build())
        {
            logAction = action.AsSpan().ToArray();
        }
        await AppendAdminLogEventAsync(id, currentUserId, logAction,
            (int)DateTimeOffset.Now.ToUnixTimeSeconds(), $"{oldUsername} {username}");

        await _unitOfWork.SaveAsync();

        string title;
        {
            using var stored = new TLChat(updatedChannelBytes, 0, updatedChannelBytes.Length);
            title = Encoding.UTF8.GetString(stored.AsChannel().Title);
        }
        if (username.Length > 0)
        {
            await _search.IndexChat(new ChatSearchModel(id, username, title));
        }
        else
        {
            await _search.DeleteChat(id);
        }

        await _fanout.PushUpdateChannelToOtherMembersAsync(id, currentUserId);
        _log.Debug($"📣 UpdateUsername user:{currentUserId} channel:{id} username:'{username}'");
        return new BoolTrue();
    }
}
