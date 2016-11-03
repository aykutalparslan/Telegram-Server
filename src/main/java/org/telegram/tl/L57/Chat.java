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

public class Chat extends org.telegram.tl.TLChat {

    public static final int ID = 0xd91cdd54;

    public int flags;
    public int id;
    public String title;
    public org.telegram.tl.TLChatPhoto photo;
    public int participants_count;
    public int date;
    public int version;
    public org.telegram.tl.TLInputChannel migrated_to;

    public Chat() {
    }

    public Chat(int flags, int id, String title, org.telegram.tl.TLChatPhoto photo, int participants_count, int date, int version, org.telegram.tl.TLInputChannel migrated_to) {
        this.flags = flags;
        this.id = id;
        this.title = title;
        this.photo = photo;
        this.participants_count = participants_count;
        this.date = date;
        this.version = version;
        this.migrated_to = migrated_to;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = buffer.readInt();
        title = buffer.readString();
        photo = (org.telegram.tl.TLChatPhoto) buffer.readTLObject(APIContext.getInstance());
        participants_count = buffer.readInt();
        date = buffer.readInt();
        version = buffer.readInt();
        if ((flags & (1 << 6)) != 0) {
            migrated_to = (org.telegram.tl.TLInputChannel) buffer.readTLObject(APIContext.getInstance());
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(96);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (migrated_to != null) {
            flags |= (1 << 6);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeInt(id);
        buff.writeString(title);
        buff.writeTLObject(photo);
        buff.writeInt(participants_count);
        buff.writeInt(date);
        buff.writeInt(version);
        if ((flags & (1 << 6)) != 0) {
            buff.writeTLObject(migrated_to);
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

    public boolean is_admins_enabled() {
        return (flags & (1 << 3)) != 0;
    }

    public void set_admins_enabled(boolean v) {
        if (v) {
            flags |= (1 << 3);
        } else {
            flags &= ~(1 << 3);
        }
    }

    public boolean is_admin() {
        return (flags & (1 << 4)) != 0;
    }

    public void set_admin(boolean v) {
        if (v) {
            flags |= (1 << 4);
        } else {
            flags &= ~(1 << 4);
        }
    }

    public boolean is_deactivated() {
        return (flags & (1 << 5)) != 0;
    }

    public void set_deactivated(boolean v) {
        if (v) {
            flags |= (1 << 5);
        } else {
            flags &= ~(1 << 5);
        }
    }

    public int getConstructor() {
        return ID;
    }
}