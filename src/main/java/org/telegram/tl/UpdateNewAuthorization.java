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

public class UpdateNewAuthorization extends TLUpdate {

    public static final int ID = -1895411046;

    public long auth_key_id;
    public int date;
    public String device;
    public String location;

    public UpdateNewAuthorization() {
    }

    public UpdateNewAuthorization(long auth_key_id, int date, String device, String location){
        this.auth_key_id = auth_key_id;
        this.date = date;
        this.device = device;
        this.location = location;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        auth_key_id = buffer.readLong();
        date = buffer.readInt();
        device = buffer.readString();
        location = buffer.readString();
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
        buff.writeLong(auth_key_id);
        buff.writeInt(date);
        buff.writeString(device);
        buff.writeString(location);
    }

    public int getConstructor() {
        return ID;
    }
}