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

public class Channel extends org.telegram.tl.TLChat {

    public static final int ID = 0xa14dca52;

    public int flags;
    public int id;
    public long access_hash;
    public String title;
    public String username;
    public org.telegram.tl.TLChatPhoto photo;
    public int date;
    public int version;
    public String restriction_reason;

    public Channel() {
    }

    public Channel(int flags, int id, long access_hash, String title, String username, org.telegram.tl.TLChatPhoto photo, int date, int version, String restriction_reason) {
        this.flags = flags;
        this.id = id;
        this.access_hash = access_hash;
        this.title = title;
        this.username = username;
        this.photo = photo;
        this.date = date;
        this.version = version;
        this.restriction_reason = restriction_reason;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = buffer.readInt();
        if ((flags & (1 << 13)) != 0) {
            access_hash = buffer.readLong();
        }
        title = buffer.readString();
        if ((flags & (1 << 6)) != 0) {
            username = buffer.readString();
        }
        photo = (org.telegram.tl.TLChatPhoto) buffer.readTLObject(APIContext.getInstance());
        date = buffer.readInt();
        version = buffer.readInt();
        if ((flags & (1 << 9)) != 0) {
            restriction_reason = buffer.readString();
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(156);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (access_hash != 0) {
            flags |= (1 << 13);
        }
        if (username != null && !username.isEmpty()) {
            flags |= (1 << 6);
        }
        if (restriction_reason != null && !restriction_reason.isEmpty()) {
            flags |= (1 << 9);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeInt(id);
        if ((flags & (1 << 13)) != 0) {
            buff.writeLong(access_hash);
        }
        buff.writeString(title);
        if ((flags & (1 << 6)) != 0) {
            buff.writeString(username);
        }
        buff.writeTLObject(photo);
        buff.writeInt(date);
        buff.writeInt(version);
        if ((flags & (1 << 9)) != 0) {
            buff.writeString(restriction_reason);
        }
    }

    public boolean is_creator() {
        return (flags & (1 << 0)) != 0;
    }

    public void set_creator(boolean v) {
        if (v) {
            flags |= (1 << 0);
        } else {
            flags &= ~(1 << 0);
        }
    }

    public boolean is_kicked() {
        return (flags & (1 << 1)) != 0;
    }

    public void set_kicked(boolean v) {
        if (v) {
            flags |= (1 << 1);
        } else {
            flags &= ~(1 << 1);
        }
    }

    public boolean is_left() {
        return (flags & (1 << 2)) != 0;
    }

    public void set_left(boolean v) {
        if (v) {
            flags |= (1 << 2);
        } else {
            flags &= ~(1 << 2);
        }
    }

    public boolean is_editor() {
        return (flags & (1 << 3)) != 0;
    }

    public void set_editor(boolean v) {
        if (v) {
            flags |= (1 << 3);
        } else {
            flags &= ~(1 << 3);
        }
    }

    public boolean is_moderator() {
        return (flags & (1 << 4)) != 0;
    }

    public void set_moderator(boolean v) {
        if (v) {
            flags |= (1 << 4);
        } else {
            flags &= ~(1 << 4);
        }
    }

    public boolean is_broadcast() {
        return (flags & (1 << 5)) != 0;
    }

    public void set_broadcast(boolean v) {
        if (v) {
            flags |= (1 << 5);
        } else {
            flags &= ~(1 << 5);
        }
    }

    public boolean is_verified() {
        return (flags & (1 << 7)) != 0;
    }

    public void set_verified(boolean v) {
        if (v) {
            flags |= (1 << 7);
        } else {
            flags &= ~(1 << 7);
        }
    }

    public boolean is_megagroup() {
        return (flags & (1 << 8)) != 0;
    }

    public void set_megagroup(boolean v) {
        if (v) {
            flags |= (1 << 8);
        } else {
            flags &= ~(1 << 8);
        }
    }

    public boolean is_restricted() {
        return (flags & (1 << 9)) != 0;
    }

    public void set_restricted(boolean v) {
        if (v) {
            flags |= (1 << 9);
        } else {
            flags &= ~(1 << 9);
        }
    }

    public boolean is_democracy() {
        return (flags & (1 << 10)) != 0;
    }

    public void set_democracy(boolean v) {
        if (v) {
            flags |= (1 << 10);
        } else {
            flags &= ~(1 << 10);
        }
    }

    public boolean is_signatures() {
        return (flags & (1 << 11)) != 0;
    }

    public void set_signatures(boolean v) {
        if (v) {
            flags |= (1 << 11);
        } else {
            flags &= ~(1 << 11);
        }
    }

    public boolean is_min() {
        return (flags & (1 << 12)) != 0;
    }

    public void set_min(boolean v) {
        if (v) {
            flags |= (1 << 12);
        } else {
            flags &= ~(1 << 12);
        }
    }

    public int getConstructor() {
        return ID;
    }
}