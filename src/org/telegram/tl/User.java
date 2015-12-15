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

package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;

public class User extends TLUser {

    public static final int ID = 0x22e49072;

    public int flags;
    public int id;
    public long access_hash;
    public String first_name;
    public String last_name;
    public String username;
    public String phone;
    public TLUserProfilePhoto photo;
    public TLUserStatus status;
    public int bot_info_version;

    public User() {
    }

    public User(int flags, int id, long access_hash, String first_name, String last_name, String username,
                String phone, TLUserProfilePhoto photo, TLUserStatus status, int bot_info_version) {
        this.flags = id;
        this.id = id;
        this.first_name = first_name;
        this.last_name = last_name;
        this.username = username;
        this.access_hash = access_hash;
        this.phone = phone;
        this.photo = photo;
        this.status = status;
        this.bot_info_version = bot_info_version;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = buffer.readInt();
        if ((flags & 1) != 0) {
            access_hash = buffer.readLong();
        }
        if ((flags & 2) != 0) {
            first_name = buffer.readString();
        }
        if ((flags & 4) != 0) {
            last_name = buffer.readString();
        }
        if ((flags & 8) != 0) {
            username = buffer.readString();
        }
        if ((flags & 16) != 0) {
            phone = buffer.readString();
        }
        if ((flags & 32) != 0) {
            photo = (TLUserProfilePhoto) buffer.readTLObject(APIContext.getInstance());
        }
        if ((flags & 64) != 0) {
            status = (TLUserStatus) buffer.readTLObject(APIContext.getInstance());
        }
        if ((flags & 16384) != 0) {
            bot_info_version = buffer.readInt();
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(32);
        serializeTo(buffer);
        return buffer;
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeInt(id);
        if ((flags & 1) != 0) {
            buff.writeLong(access_hash);
        }
        if ((flags & 2) != 0) {
            buff.writeString(first_name);
        }
        if ((flags & 4) != 0) {
            buff.writeString(last_name);
        }
        if ((flags & 8) != 0) {
            buff.writeString(username);
        }
        if ((flags & 16) != 0) {
            buff.writeString(phone);
        }
        if ((flags & 32) != 0) {
            buff.writeTLObject(photo);
        }
        if ((flags & 64) != 0) {
            buff.writeTLObject(status);
        }
        if ((flags & 16384) != 0) {
            buff.writeInt(bot_info_version);
        }
    }

    public int getConstructor() {
        return ID;
    }
}