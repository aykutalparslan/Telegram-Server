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

public class User extends org.telegram.tl.TLUser {

    public static final int ID = 0xd10d979a;

    public int flags;
    public int id;
    public long access_hash;
    public String first_name;
    public String last_name;
    public String username;
    public String phone;
    public org.telegram.tl.TLUserProfilePhoto photo;
    public org.telegram.tl.TLUserStatus status;
    public int bot_info_version;
    public String restriction_reason;
    public String bot_inline_placeholder;

    public User() {
    }

    public User(int flags, int id, long access_hash, String first_name, String last_name, String username, String phone, org.telegram.tl.TLUserProfilePhoto photo, org.telegram.tl.TLUserStatus status, int bot_info_version, String restriction_reason, String bot_inline_placeholder) {
        this.flags = flags;
        this.id = id;
        this.access_hash = access_hash;
        this.first_name = first_name;
        this.last_name = last_name;
        this.username = username;
        this.phone = phone;
        this.photo = photo;
        this.status = status;
        this.bot_info_version = bot_info_version;
        this.restriction_reason = restriction_reason;
        this.bot_inline_placeholder = bot_inline_placeholder;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = buffer.readInt();
        if ((flags & (1 << 0)) != 0) {
            access_hash = buffer.readLong();
        }
        if ((flags & (1 << 1)) != 0) {
            first_name = buffer.readString();
        }
        if ((flags & (1 << 2)) != 0) {
            last_name = buffer.readString();
        }
        if ((flags & (1 << 3)) != 0) {
            username = buffer.readString();
        }
        if ((flags & (1 << 4)) != 0) {
            phone = buffer.readString();
        }
        if ((flags & (1 << 5)) != 0) {
            photo = (org.telegram.tl.TLUserProfilePhoto) buffer.readTLObject(APIContext.getInstance());
        }
        if ((flags & (1 << 6)) != 0) {
            status = (org.telegram.tl.TLUserStatus) buffer.readTLObject(APIContext.getInstance());
        }
        if ((flags & (1 << 14)) != 0) {
            bot_info_version = buffer.readInt();
        }
        if ((flags & (1 << 18)) != 0) {
            restriction_reason = buffer.readString();
        }
        if ((flags & (1 << 19)) != 0) {
            bot_inline_placeholder = buffer.readString();
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(180);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (access_hash != 0) {
            flags |= (1 << 0);
        }
        if (first_name != null && !first_name.isEmpty()) {
            flags |= (1 << 1);
        }
        if (last_name != null && !last_name.isEmpty()) {
            flags |= (1 << 2);
        }
        if (username != null && !username.isEmpty()) {
            flags |= (1 << 3);
        }
        if (phone != null && !phone.isEmpty()) {
            flags |= (1 << 4);
        }
        if (photo != null) {
            flags |= (1 << 5);
        }
        if (status != null) {
            flags |= (1 << 6);
        }
        if (bot_info_version != 0) {
            flags |= (1 << 14);
        }
        if (restriction_reason != null && !restriction_reason.isEmpty()) {
            flags |= (1 << 18);
        }
        if (bot_inline_placeholder != null && !bot_inline_placeholder.isEmpty()) {
            flags |= (1 << 19);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeInt(id);
        if ((flags & (1 << 0)) != 0) {
            buff.writeLong(access_hash);
        }
        if ((flags & (1 << 1)) != 0) {
            buff.writeString(first_name);
        }
        if ((flags & (1 << 2)) != 0) {
            buff.writeString(last_name);
        }
        if ((flags & (1 << 3)) != 0) {
            buff.writeString(username);
        }
        if ((flags & (1 << 4)) != 0) {
            buff.writeString(phone);
        }
        if ((flags & (1 << 5)) != 0) {
            buff.writeTLObject(photo);
        }
        if ((flags & (1 << 6)) != 0) {
            buff.writeTLObject(status);
        }
        if ((flags & (1 << 14)) != 0) {
            buff.writeInt(bot_info_version);
        }
        if ((flags & (1 << 18)) != 0) {
            buff.writeString(restriction_reason);
        }
        if ((flags & (1 << 19)) != 0) {
            buff.writeString(bot_inline_placeholder);
        }
    }

    public boolean is_self() {
        return (flags & (1 << 10)) != 0;
    }

    public void set_self(boolean v) {
        if (v) {
            flags |= (1 << 10);
        } else {
            flags &= ~(1 << 10);
        }
    }

    public boolean is_contact() {
        return (flags & (1 << 11)) != 0;
    }

    public void set_contact(boolean v) {
        if (v) {
            flags |= (1 << 11);
        } else {
            flags &= ~(1 << 11);
        }
    }

    public boolean is_mutual_contact() {
        return (flags & (1 << 12)) != 0;
    }

    public void set_mutual_contact(boolean v) {
        if (v) {
            flags |= (1 << 12);
        } else {
            flags &= ~(1 << 12);
        }
    }

    public boolean is_deleted() {
        return (flags & (1 << 13)) != 0;
    }

    public void set_deleted(boolean v) {
        if (v) {
            flags |= (1 << 13);
        } else {
            flags &= ~(1 << 13);
        }
    }

    public boolean is_bot() {
        return (flags & (1 << 14)) != 0;
    }

    public void set_bot(boolean v) {
        if (v) {
            flags |= (1 << 14);
        } else {
            flags &= ~(1 << 14);
        }
    }

    public boolean is_bot_chat_history() {
        return (flags & (1 << 15)) != 0;
    }

    public void set_bot_chat_history(boolean v) {
        if (v) {
            flags |= (1 << 15);
        } else {
            flags &= ~(1 << 15);
        }
    }

    public boolean is_bot_nochats() {
        return (flags & (1 << 16)) != 0;
    }

    public void set_bot_nochats(boolean v) {
        if (v) {
            flags |= (1 << 16);
        } else {
            flags &= ~(1 << 16);
        }
    }

    public boolean is_verified() {
        return (flags & (1 << 17)) != 0;
    }

    public void set_verified(boolean v) {
        if (v) {
            flags |= (1 << 17);
        } else {
            flags &= ~(1 << 17);
        }
    }

    public boolean is_restricted() {
        return (flags & (1 << 18)) != 0;
    }

    public void set_restricted(boolean v) {
        if (v) {
            flags |= (1 << 18);
        } else {
            flags &= ~(1 << 18);
        }
    }

    public boolean is_min() {
        return (flags & (1 << 20)) != 0;
    }

    public void set_min(boolean v) {
        if (v) {
            flags |= (1 << 20);
        } else {
            flags &= ~(1 << 20);
        }
    }

    public boolean is_bot_inline_geo() {
        return (flags & (1 << 21)) != 0;
    }

    public void set_bot_inline_geo(boolean v) {
        if (v) {
            flags |= (1 << 21);
        } else {
            flags &= ~(1 << 21);
        }
    }

    public int getConstructor() {
        return ID;
    }
}