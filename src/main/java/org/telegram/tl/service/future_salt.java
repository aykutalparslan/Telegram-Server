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

package org.telegram.tl.service;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class future_salt extends TLObject {

    public static final int ID = 0x0949d9dc;

    public int valid_since;
    public int valid_until;
    public long salt;

    public future_salt() {

    }

    public future_salt(int valid_since, int valid_until, long salt){
        this.valid_since = valid_since;
        this.valid_until = valid_until;
        this.salt = salt;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        valid_since = buffer.readInt();
        valid_until = buffer.readInt();
        salt = buffer.readLong();
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
        buff.writeInt(valid_since);
        buff.writeInt(valid_until);
        buff.writeLong(salt);
    }

    public int getConstructor() {
        return ID;
    }
}