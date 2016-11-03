/*
 *     This file is part of Telegram Server
 *     Copyright (C) 2015  Aykut Alparslan KOÇ
 *
 *     Telegram Server is free software: you can redistribute it and/or modify
 *     it under the terms of the GNU General Public License as published by
 *     the Free Software Foundation, either version 3 of the License, or
 *     (at your option) any later version.
 *
 *     Telegram Server is distributed in the hope that it will be useful,
 *     but WITHOUT ANY WARRANTY; without even the implied warranty of
 *     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *     GNU General Public License for more details.
 *
 *     You should have received a copy of the GNU General Public License
 *     along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ChannelFull extends org.telegram.tl.messages.TLChatFull {

    public static final int ID = 0xc3d5512f;

    public int flags;
    public int id;
    public String about;
    public int participants_count;
    public int admins_count;
    public int kicked_count;
    public int read_inbox_max_id;
    public int read_outbox_max_id;
    public int unread_count;
    public org.telegram.tl.photos.TLPhoto chat_photo;
    public org.telegram.tl.TLPeerNotifySettings notify_settings;
    public org.telegram.tl.TLExportedChatInvite exported_invite;
    public TLVector<org.telegram.tl.TLBotInfo> bot_info;
    public int migrated_from_chat_id;
    public int migrated_from_max_id;
    public int pinned_msg_id;

    public ChannelFull() {
        this.bot_info = new TLVector<>();
    }

    public ChannelFull(int flags, int id, String about, int participants_count, int admins_count, int kicked_count, int read_inbox_max_id, int read_outbox_max_id, int unread_count, org.telegram.tl.photos.TLPhoto chat_photo, org.telegram.tl.TLPeerNotifySettings notify_settings, org.telegram.tl.TLExportedChatInvite exported_invite, TLVector<org.telegram.tl.TLBotInfo> bot_info, int migrated_from_chat_id, int migrated_from_max_id, int pinned_msg_id) {
        this.flags = flags;
        this.id = id;
        this.about = about;
        this.participants_count = participants_count;
        this.admins_count = admins_count;
        this.kicked_count = kicked_count;
        this.read_inbox_max_id = read_inbox_max_id;
        this.read_outbox_max_id = read_outbox_max_id;
        this.unread_count = unread_count;
        this.chat_photo = chat_photo;
        this.notify_settings = notify_settings;
        this.exported_invite = exported_invite;
        this.bot_info = bot_info;
        this.migrated_from_chat_id = migrated_from_chat_id;
        this.migrated_from_max_id = migrated_from_max_id;
        this.pinned_msg_id = pinned_msg_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = buffer.readInt();
        about = buffer.readString();
        if ((flags & (1 << 0)) != 0) {
            participants_count = buffer.readInt();
        }
        if ((flags & (1 << 1)) != 0) {
            admins_count = buffer.readInt();
        }
        if ((flags & (1 << 2)) != 0) {
            kicked_count = buffer.readInt();
        }
        read_inbox_max_id = buffer.readInt();
        read_outbox_max_id = buffer.readInt();
        unread_count = buffer.readInt();
        chat_photo = (org.telegram.tl.photos.TLPhoto) buffer.readTLObject(APIContext.getInstance());
        notify_settings = (org.telegram.tl.TLPeerNotifySettings) buffer.readTLObject(APIContext.getInstance());
        exported_invite = (org.telegram.tl.TLExportedChatInvite) buffer.readTLObject(APIContext.getInstance());
        bot_info = (TLVector<org.telegram.tl.TLBotInfo>) buffer.readTLObject(APIContext.getInstance());
        if ((flags & (1 << 4)) != 0) {
            migrated_from_chat_id = buffer.readInt();
        }
        if ((flags & (1 << 4)) != 0) {
            migrated_from_max_id = buffer.readInt();
        }
        if ((flags & (1 << 5)) != 0) {
            pinned_msg_id = buffer.readInt();
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(128);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (participants_count != 0) {
            flags |= (1 << 0);
        }
        if (admins_count != 0) {
            flags |= (1 << 1);
        }
        if (kicked_count != 0) {
            flags |= (1 << 2);
        }
        if (migrated_from_chat_id != 0) {
            flags |= (1 << 4);
        }
        if (migrated_from_max_id != 0) {
            flags |= (1 << 4);
        }
        if (pinned_msg_id != 0) {
            flags |= (1 << 5);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeInt(id);
        buff.writeString(about);
        if ((flags & (1 << 0)) != 0) {
            buff.writeInt(participants_count);
        }
        if ((flags & (1 << 1)) != 0) {
            buff.writeInt(admins_count);
        }
        if ((flags & (1 << 2)) != 0) {
            buff.writeInt(kicked_count);
        }
        buff.writeInt(read_inbox_max_id);
        buff.writeInt(read_outbox_max_id);
        buff.writeInt(unread_count);
        buff.writeTLObject(chat_photo);
        buff.writeTLObject(notify_settings);
        buff.writeTLObject(exported_invite);
        buff.writeTLObject(bot_info);
        if ((flags & (1 << 4)) != 0) {
            buff.writeInt(migrated_from_chat_id);
        }
        if ((flags & (1 << 4)) != 0) {
            buff.writeInt(migrated_from_max_id);
        }
        if ((flags & (1 << 5)) != 0) {
            buff.writeInt(pinned_msg_id);
        }
    }

    public boolean is_can_view_participants() {
        return (flags & (1 << 3)) != 0;
    }

    public void set_can_view_participants(boolean v) {
        if (v) {
            flags |= (1 << 3);
        } else {
            flags &= ~(1 << 3);
        }
    }

    public boolean is_can_set_username() {
        return (flags & (1 << 6)) != 0;
    }

    public void set_can_set_username(boolean v) {
        if (v) {
            flags |= (1 << 6);
        } else {
            flags &= ~(1 << 6);
        }
    }

    public int getConstructor() {
        return ID;
    }
}