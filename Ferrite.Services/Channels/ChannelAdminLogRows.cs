// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Channels;

public static class ChannelAdminLogRows
{
    private const int Join = 1 << 0;
    private const int Leave = 1 << 1;
    private const int Invite = 1 << 2;
    private const int Ban = 1 << 3;
    private const int Unban = 1 << 4;
    private const int Kick = 1 << 5;
    private const int Unkick = 1 << 6;
    private const int Promote = 1 << 7;
    private const int Demote = 1 << 8;
    private const int Info = 1 << 9;
    private const int Settings = 1 << 10;
    private const int Pinned = 1 << 11;
    private const int Edit = 1 << 12;
    private const int Delete = 1 << 13;
    private const int GroupCall = 1 << 14;
    private const int Invites = 1 << 15;
    private const int Send = 1 << 16;
    private const int Forums = 1 << 17;
    private const int SubExtend = 1 << 18;

    private const int Restrictions = Ban | Unban | Kick | Unkick;

    private const int Promotions = Promote | Demote;

    public enum BoolActionKind
    {
        PreHistoryHidden,
        AntiSpam,
        Autotranslation,
        Signatures,
        SignatureProfiles,
    }

    public static byte[] BoolAction(BoolActionKind kind, bool newValue)
    {
        using TLChannelAdminLogEventAction action = kind switch
        {
            BoolActionKind.PreHistoryHidden =>
                ChannelAdminLogEventActionTogglePreHistoryHidden.Builder()
                    .NewValue(newValue).Build(),
            BoolActionKind.AntiSpam =>
                ChannelAdminLogEventActionToggleAntiSpam.Builder()
                    .NewValue(newValue).Build(),
            BoolActionKind.Autotranslation =>
                ChannelAdminLogEventActionToggleAutotranslation.Builder()
                    .NewValue(newValue).Build(),
            BoolActionKind.Signatures =>
                ChannelAdminLogEventActionToggleSignatures.Builder()
                    .NewValue(newValue).Build(),
            _ => ChannelAdminLogEventActionToggleSignatureProfiles.Builder()
                .NewValue(newValue).Build(),
        };
        return action.AsSpan().ToArray();
    }

    public static TLAdminLogEvent Build(long channelId, long id, int date,
        long userId, ReadOnlySpan<byte> action, string searchText) =>
        AdminLogEvent.Builder()
            .ChannelId(channelId)
            .Id(id)
            .Date(date)
            .UserId(userId)
            .Action(action)
            .SearchText(Encoding.UTF8.GetBytes(searchText))
            .Build();

    public static int FilterMask(int actionConstructor) => actionConstructor switch
    {
        Constructors.baseLayer_ChannelAdminLogEventActionParticipantJoin => Join,
        Constructors.baseLayer_ChannelAdminLogEventActionParticipantJoinByInvite => Join,
        Constructors.baseLayer_ChannelAdminLogEventActionParticipantJoinByRequest => Join,
        Constructors.baseLayer_ChannelAdminLogEventActionParticipantLeave => Leave,
        Constructors.baseLayer_ChannelAdminLogEventActionParticipantInvite => Invite,
        Constructors.baseLayer_ChannelAdminLogEventActionParticipantToggleBan => Restrictions,
        Constructors.baseLayer_ChannelAdminLogEventActionDefaultBannedRights => Restrictions,
        Constructors.baseLayer_ChannelAdminLogEventActionParticipantToggleAdmin => Promotions,
        Constructors.baseLayer_ChannelAdminLogEventActionChangeTitle => Info,
        Constructors.baseLayer_ChannelAdminLogEventActionChangeAbout => Info,
        Constructors.baseLayer_ChannelAdminLogEventActionChangePhoto => Info,
        Constructors.baseLayer_ChannelAdminLogEventActionChangeUsername => Info,
        Constructors.baseLayer_ChannelAdminLogEventActionChangeUsernames => Info,
        Constructors.baseLayer_ChannelAdminLogEventActionChangeLinkedChat => Info,
        Constructors.baseLayer_ChannelAdminLogEventActionChangeLocation => Info,
        Constructors.baseLayer_ChannelAdminLogEventActionChangePeerColor => Info,
        Constructors.baseLayer_ChannelAdminLogEventActionChangeProfilePeerColor => Info,
        Constructors.baseLayer_ChannelAdminLogEventActionChangeEmojiStatus => Info,
        Constructors.baseLayer_ChannelAdminLogEventActionChangeWallpaper => Info,
        Constructors.baseLayer_ChannelAdminLogEventActionChangeStickerSet => Info,
        Constructors.baseLayer_ChannelAdminLogEventActionChangeEmojiStickerSet => Info,
        Constructors.baseLayer_ChannelAdminLogEventActionToggleInvites => Settings,
        Constructors.baseLayer_ChannelAdminLogEventActionToggleSignatures => Settings,
        Constructors.baseLayer_ChannelAdminLogEventActionToggleSignatureProfiles => Settings,
        Constructors.baseLayer_ChannelAdminLogEventActionTogglePreHistoryHidden => Settings,
        Constructors.baseLayer_ChannelAdminLogEventActionToggleSlowMode => Settings,
        Constructors.baseLayer_ChannelAdminLogEventActionToggleAntiSpam => Settings,
        Constructors.baseLayer_ChannelAdminLogEventActionToggleAutotranslation => Settings,
        Constructors.baseLayer_ChannelAdminLogEventActionToggleNoForwards => Settings,
        Constructors.baseLayer_ChannelAdminLogEventActionChangeHistoryTTL => Settings,
        Constructors.baseLayer_ChannelAdminLogEventActionChangeAvailableReactions => Settings,
        Constructors.baseLayer_ChannelAdminLogEventActionUpdatePinned => Pinned,
        Constructors.baseLayer_ChannelAdminLogEventActionEditMessage => Edit,
        Constructors.baseLayer_ChannelAdminLogEventActionStopPoll => Edit,
        Constructors.baseLayer_ChannelAdminLogEventActionDeleteMessage => Delete,
        Constructors.baseLayer_ChannelAdminLogEventActionStartGroupCall => GroupCall,
        Constructors.baseLayer_ChannelAdminLogEventActionDiscardGroupCall => GroupCall,
        Constructors.baseLayer_ChannelAdminLogEventActionParticipantMute => GroupCall,
        Constructors.baseLayer_ChannelAdminLogEventActionParticipantUnmute => GroupCall,
        Constructors.baseLayer_ChannelAdminLogEventActionParticipantVolume => GroupCall,
        Constructors.baseLayer_ChannelAdminLogEventActionToggleGroupCallSetting => GroupCall,
        Constructors.baseLayer_ChannelAdminLogEventActionExportedInviteDelete => Invites,
        Constructors.baseLayer_ChannelAdminLogEventActionExportedInviteRevoke => Invites,
        Constructors.baseLayer_ChannelAdminLogEventActionExportedInviteEdit => Invites,
        Constructors.baseLayer_ChannelAdminLogEventActionSendMessage => Send,
        Constructors.baseLayer_ChannelAdminLogEventActionToggleForum => Forums,
        Constructors.baseLayer_ChannelAdminLogEventActionCreateTopic => Forums,
        Constructors.baseLayer_ChannelAdminLogEventActionEditTopic => Forums,
        Constructors.baseLayer_ChannelAdminLogEventActionDeleteTopic => Forums,
        Constructors.baseLayer_ChannelAdminLogEventActionPinTopic => Forums,
        Constructors.baseLayer_ChannelAdminLogEventActionParticipantSubExtend => SubExtend,
        _ => 0,
    };

    public static int RequestedMask(ChannelAdminLogEventsFilter filter)
    {
        Flags flags = filter.Flags;
        int mask = 0;
        for (int bit = 0; bit <= 18; bit++)
        {
            if (flags[bit])
            {
                mask |= 1 << bit;
            }
        }
        return mask;
    }
}
