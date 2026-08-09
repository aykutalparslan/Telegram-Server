// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer;

namespace Ferrite.Services;

// Compact dialog-box key: the (peer-type, id) pair that addresses a per-user
// dialog/message box. Promoted to a shared type so peer resolution lives here.
public readonly record struct DialogPeerKey(TLPeer.PeerType Type, long Id);

// Pure InputPeer/InputUser -> resolved peer/user/chat translation. All reads go
// through generated slim views and never touch service state or repositories.
public static class PeerResolver
{
    public static bool TryReadPeer(PeerView peer,
        out (TLPeer.PeerType Type, long Id) value)
    {
        if (peer.Is(out PeerUser user))
        {
            value = (TLPeer.PeerType.PeerUser, user.UserId);
            return true;
        }
        if (peer.Is(out PeerChat chat))
        {
            value = (TLPeer.PeerType.PeerChat, chat.ChatId);
            return true;
        }
        if (peer.Is(out PeerChannel channel))
        {
            value = (TLPeer.PeerType.PeerChannel, channel.ChannelId);
            return true;
        }

        value = default;
        return false;
    }

    public static TLPeer PeerFromInputPeer(InputPeerView p, long selfUserId = 0)=> p.Type switch
    {
        TLInputPeer.InputPeerType.InputPeerSelf => new PeerUser(selfUserId),
        TLInputPeer.InputPeerType.InputPeerChat => new PeerChat(p.AsInputPeerChat().ChatId),
        TLInputPeer.InputPeerType.InputPeerUser => new PeerUser(p.AsInputPeerUser().UserId),
        TLInputPeer.InputPeerType.InputPeerChannel => new PeerChannel(p.AsInputPeerChannel().ChannelId),
        TLInputPeer.InputPeerType.InputPeerUserFromMessage => new PeerUser(p.AsInputPeerUserFromMessage().UserId),
        TLInputPeer.InputPeerType.InputPeerChannelFromMessage => new PeerChannel(p.AsInputPeerChannelFromMessage().ChannelId),
        _ => new PeerUser(0)
    };

    // History/read/delete paths accept private and basic-group peers. Unknown peer
    // kinds resolve to an empty user conversation instead
    // of throwing, so client flows degrade to empty results rather than stalling.
    public static (TLPeer.PeerType Type, long Id) ResolveHistoryPeer(InputPeerView peer,
        long selfUserId)
    {
        if (peer.Is(out InputPeerSelf _)) return (TLPeer.PeerType.PeerUser, selfUserId);
        if (peer.Is(out InputPeerUser user)) return (TLPeer.PeerType.PeerUser, user.UserId);
        if (peer.Is(out InputPeerChat chat)) return (TLPeer.PeerType.PeerChat, chat.ChatId);
        return (TLPeer.PeerType.PeerUser, 0);
    }

    public static (bool IsChannel, long ChatId) ResolveInviteChatPeer(InputPeerView peer)
    {
        if (peer.Is(out InputPeerChat chatPeer))
        {
            return (false, chatPeer.ChatId);
        }
        if (peer.Is(out InputPeerChannel channelPeer))
        {
            return (true, channelPeer.ChannelId);
        }
        if (peer.Is(out InputPeerChannelFromMessage fromMessage))
        {
            return (true, fromMessage.ChannelId);
        }

        return (false, 0);
    }

    public static (bool IsSelf, long UserId) ReadInputUser(InputUserView user)
    {
        if (user.Is(out InputUserSelf _))
        {
            return (true, 0);
        }
        if (user.Is(out InputUser inputUser))
        {
            return (false, inputUser.UserId);
        }
        if (user.Is(out InputUserFromMessage fromMessage))
        {
            return (false, fromMessage.UserId);
        }

        return (false, 0);
    }

    public static long ResolveInputPeerChannelId(InputPeerView peer)
    {
        if (peer.Is(out InputPeerChannel channel)) return channel.ChannelId;
        if (peer.Is(out InputPeerChannelFromMessage fromMessage)) return fromMessage.ChannelId;
        return 0;
    }

    public static TLPeer BuildPeer(TLPeer.PeerType peerType, long peerId)
    {
        if (peerType == TLPeer.PeerType.PeerChat)
        {
            return PeerChat.Builder().ChatId(peerId).Build();
        }
        if (peerType == TLPeer.PeerType.PeerChannel)
        {
            return PeerChannel.Builder().ChannelId(peerId).Build();
        }

        return PeerUser.Builder().UserId(peerId).Build();
    }

    public static DialogPeerKey? ResolveOptionalDialogPeer(InputPeerView peer,
        long selfUserId)
    {
        if (peer.Is(out InputPeerEmpty _)) return null;
        return TryResolveInputPeerDialogKey(peer, selfUserId, out var key) ? key : null;
    }

    public static bool TryResolveInputPeerDialogKey(InputPeerView peer, long selfUserId,
        out DialogPeerKey key)
    {
        if (peer.Is(out InputPeerSelf _))
        {
            key = new DialogPeerKey(TLPeer.PeerType.PeerUser, selfUserId);
            return true;
        }
        if (peer.Is(out InputPeerUser user))
        {
            key = new DialogPeerKey(TLPeer.PeerType.PeerUser, user.UserId);
            return true;
        }
        if (peer.Is(out InputPeerChat chat))
        {
            key = new DialogPeerKey(TLPeer.PeerType.PeerChat, chat.ChatId);
            return true;
        }
        if (peer.Is(out InputPeerChannel channel))
        {
            key = new DialogPeerKey(TLPeer.PeerType.PeerChannel, channel.ChannelId);
            return true;
        }

        key = default;
        return false;
    }

    public static bool TryResolveInputDialogPeerKey(InputDialogPeerView peer,
        long selfUserId, out DialogPeerKey key)
    {
        if (peer.Is(out InputDialogPeer dialogPeer))
        {
            return TryResolveInputPeerDialogKey(dialogPeer.Get_PeerView(), selfUserId,
                out key);
        }
        key = default;
        return false;
    }
}
