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
import org.telegram.tl.*;

public class UserRequest extends TLUser {

    public static final int ID = -640891665;

    public int id;
    public String first_name;
    public String last_name;
    public String username;
    public long access_hash;
    public String phone;
    public TLUserProfilePhoto photo;
    public TLUserStatus status;

    public UserRequest() {
    }

    public UserRequest(int id, String first_name, String last_name, String username, long access_hash, String phone, TLUserProfilePhoto photo, TLUserStatus status){
        this.id = id;
        this.first_name = first_name;
        this.last_name = last_name;
        this.username = username;
        this.access_hash = access_hash;
        this.phone = phone;
        this.photo = photo;
        this.status = status;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readInt();
        first_name = buffer.readString();
        last_name = buffer.readString();
        username = buffer.readString();
        access_hash = buffer.readLong();
        phone = buffer.readString();
        photo = (TLUserProfilePhoto) buffer.readTLObject(APIContext.getInstance());
        status = (TLUserStatus) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeInt(id);
        buff.writeString(first_name);
        buff.writeString(last_name);
        buff.writeString(username);
        buff.writeLong(access_hash);
        buff.writeString(phone);
        buff.writeTLObject(photo);
        buff.writeTLObject(status);
    }

    public int getConstructor() {
        return ID;
    }
}