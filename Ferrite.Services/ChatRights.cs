// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

// Granular chatAdminRights requirements enforced at touch points.
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

// Member actions gated by chatBannedRights (participant restrictions and the
// chat/channel default banned rights).
public enum ChatBannedAction
{
    SendMessages,
    SendPhotos,
    SendDocuments,
    SendPolls,
    InviteUsers,
}

// Synchronous rights checks over the stored participant/chat rows. All reads go
// through generated slim views and never cross an await inside these helpers.
public static class ChatRights
{
    // Creator always passes. An admin row without persisted admin rights predates
    // rights management and keeps the historical full-rights behavior.
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

    // Overload for callers that carried the participant row across an await as bytes.
    public static bool HasAdminRight(byte[] participantRowBytes,
        ChatAdminRightRequirement required)
    {
        using var row = new TLChatParticipantInfo(participantRowBytes, 0,
            participantRowBytes.Length);
        return HasAdminRight(row, required);
    }

    // A participant restriction applies while its until_date is 0 (forever) or in
    // the future; an expired restriction no longer bans the action.
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

    // The default banned rights a newly created chat/channel carries: an EMPTY
    // ban set, which must still be SENT rather than omitted.
    //
    // An absent default_banned_rights does not mean "unrestricted" to the pinned
    // client. RestrictedRights(chatBannedRights*, ChannelType) returns flags_ = 0
    // for a null pointer (DialogParticipant.cpp:175), and those flags are an
    // ALLOW mask, so every can_send_* comes out false and no plain member can
    // post anything. Only a present row is negated ban-bit by ban-bit into
    // permissions. Creators and admins bypass default permissions, which is why
    // omitting this is invisible until a non-admin member tries to speak.
    //
    // until_date is int.MaxValue because TDLib logs an error for any other value
    // in default rights (DialogParticipant.cpp:181).
    public static byte[] BuildUnrestrictedDefaultBannedRights()
    {
        using var rights = ChatBannedRights.Builder()
            .UntilDate(int.MaxValue)
            .Build();
        return rights.ToReadOnlySpan().ToArray();
    }

    // Whether the compact chat/channel row's default banned rights ban the action
    // for plain members. Creator/admin callers bypass these at the call sites.
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

    // Blanket and granular restrictions are evaluated per outgoing content kind.
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

    // channels.editAdmin with an all-false rights object is a demotion.
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

    // channels.editBanned with an all-false rights object is an unban.
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

    // A view_messages ban is a kick; everything else is a restriction.
    public static bool BansViewMessages(byte[] bannedRightsBytes) =>
        ((ChatBannedRights)bannedRightsBytes.AsSpan()).ViewMessages;

    // Whether the requested admin rights grant anything the promoting admin does not
    // itself hold (creators, and admin rows without persisted rights, hold everything).
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
