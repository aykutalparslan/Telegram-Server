// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Chats;

public enum ChatAdminRightRequirement
{
    Any,
    ChangeInfo,
    PostMessages,
    DeleteMessages,
    BanUsers,
    InviteUsers,
    PinMessages,
    AddAdmins,
    ManageTopics,
    ManageCall,
}

public enum ChatBannedAction
{
    SendMessages,
    SendPhotos,
    SendDocuments,
    SendPolls,
    InviteUsers,
}

public static class ChatRights
{
    public static bool HasAdminRight(TLChatParticipantInfo participant,
        ChatAdminRightRequirement required)
    {
        var info = participant.AsChatParticipantInfo();
        if (info.Role == (int)ChatParticipantRole.Creator)
        {
            return true;
        }
        if (info.Role != (int)ChatParticipantRole.Admin)
        {
            return false;
        }
        if (required == ChatAdminRightRequirement.Any || !info.Flags[0] ||
            !info.Get_AdminRightsView().Is(out ChatAdminRights rights))
        {
            return true;
        }

        return required switch
        {
            ChatAdminRightRequirement.ChangeInfo => rights.ChangeInfo,
            ChatAdminRightRequirement.PostMessages => rights.PostMessages,
            ChatAdminRightRequirement.DeleteMessages => rights.DeleteMessages,
            ChatAdminRightRequirement.BanUsers => rights.BanUsers,
            ChatAdminRightRequirement.InviteUsers => rights.InviteUsers,
            ChatAdminRightRequirement.PinMessages => rights.PinMessages,
            ChatAdminRightRequirement.AddAdmins => rights.AddAdmins,
            ChatAdminRightRequirement.ManageTopics => rights.ManageTopics,
            ChatAdminRightRequirement.ManageCall => rights.ManageCall,
            _ => true,
        };
    }

    public static bool HasAdminRight(byte[] participantRowBytes,
        ChatAdminRightRequirement required)
    {
        using var row = new TLChatParticipantInfo(participantRowBytes, 0,
            participantRowBytes.Length);
        return HasAdminRight(row, required);
    }

    public static bool IsRestrictedFrom(TLChatParticipantInfo participant,
        ChatBannedAction action, int now)
    {
        var info = participant.AsChatParticipantInfo();
        if (!info.Flags[1] || !info.Get_BannedRightsView().Is(out ChatBannedRights rights))
        {
            return false;
        }
        if (rights.UntilDate != 0 && rights.UntilDate <= now)
        {
            return false;
        }

        return BansAction(rights, action);
    }

    public static byte[] BuildUnrestrictedDefaultBannedRights()
    {
        using var rights = ChatBannedRights.Builder()
            .UntilDate(int.MaxValue)
            .Build();
        return rights.ToReadOnlySpan().ToArray();
    }

    public static bool DefaultBans(byte[] chatRowBytes, ChatBannedAction action)
    {
        using var stored = new TLChat(chatRowBytes, 0, chatRowBytes.Length);
        if (stored.Type == TLChat.ChatType.Chat)
        {
            var chat = stored.AsChat();
            return chat.Flags[18] &&
                   chat.Get_DefaultBannedRightsView().Is(out ChatBannedRights rights) &&
                   BansAction(rights, action);
        }
        if (stored.Type == TLChat.ChatType.Channel)
        {
            var channel = stored.AsChannel();
            return channel.Flags[18] &&
                   channel.Get_DefaultBannedRightsView().Is(out ChatBannedRights rights) &&
                   BansAction(rights, action);
        }

        return false;
    }

    private static bool BansAction(ChatBannedRights rights, ChatBannedAction action) =>
        action switch
        {
            ChatBannedAction.SendMessages => rights.SendMessages || rights.SendPlain,
            ChatBannedAction.SendPhotos => rights.SendMessages || rights.SendMedia ||
                                           rights.SendPhotos,
            ChatBannedAction.SendDocuments => rights.SendMessages || rights.SendMedia ||
                                              rights.SendDocs,
            ChatBannedAction.SendPolls => rights.SendMessages || rights.SendMedia ||
                                          rights.SendPolls,
            ChatBannedAction.InviteUsers => rights.InviteUsers,
            _ => false,
        };

    public static bool HasAnyAdminRight(byte[] adminRightsBytes)
    {
        var rights = (ChatAdminRights)adminRightsBytes.AsSpan();
        return rights.ChangeInfo || rights.PostMessages || rights.EditMessages ||
               rights.DeleteMessages || rights.BanUsers || rights.InviteUsers ||
               rights.PinMessages || rights.AddAdmins || rights.Anonymous ||
               rights.ManageCall || rights.Other || rights.ManageTopics ||
               rights.PostStories || rights.EditStories || rights.DeleteStories ||
               rights.ManageDirectMessages;
    }

    public static bool HasAnyBannedFlag(byte[] bannedRightsBytes)
    {
        var rights = (ChatBannedRights)bannedRightsBytes.AsSpan();
        return rights.ViewMessages || rights.SendMessages || rights.SendMedia ||
               rights.SendStickers || rights.SendGifs || rights.SendGames ||
               rights.SendInline || rights.EmbedLinks || rights.SendPolls ||
               rights.ChangeInfo || rights.InviteUsers || rights.PinMessages ||
               rights.ManageTopics || rights.SendPhotos || rights.SendVideos ||
               rights.SendRoundvideos || rights.SendAudios || rights.SendVoices ||
               rights.SendDocs || rights.SendPlain;
    }

    public static bool BansViewMessages(byte[] bannedRightsBytes) =>
        ((ChatBannedRights)bannedRightsBytes.AsSpan()).ViewMessages;

    public static bool GrantsBeyondCaller(byte[] requestedRightsBytes,
        TLChatParticipantInfo caller)
    {
        var info = caller.AsChatParticipantInfo();
        if (info.Role == (int)ChatParticipantRole.Creator || !info.Flags[0] ||
            !info.Get_AdminRightsView().Is(out ChatAdminRights held))
        {
            return false;
        }

        var requested = (ChatAdminRights)requestedRightsBytes.AsSpan();
        return (requested.ChangeInfo && !held.ChangeInfo) ||
               (requested.PostMessages && !held.PostMessages) ||
               (requested.EditMessages && !held.EditMessages) ||
               (requested.DeleteMessages && !held.DeleteMessages) ||
               (requested.BanUsers && !held.BanUsers) ||
               (requested.InviteUsers && !held.InviteUsers) ||
               (requested.PinMessages && !held.PinMessages) ||
               (requested.AddAdmins && !held.AddAdmins) ||
               (requested.Anonymous && !held.Anonymous) ||
               (requested.ManageCall && !held.ManageCall) ||
               (requested.Other && !held.Other) ||
               (requested.ManageTopics && !held.ManageTopics) ||
               (requested.PostStories && !held.PostStories) ||
               (requested.EditStories && !held.EditStories) ||
               (requested.DeleteStories && !held.DeleteStories) ||
               (requested.ManageDirectMessages && !held.ManageDirectMessages);
    }
}
