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

public class new_session_created extends TLObject {

    public static final int ID = 0x9ec20908;

    public long first_msg_id;
    public long unique_id;
    public long server_salt;

    public new_session_created() {

    }

    public new_session_created(long first_msg_id, long unique_id, long server_salt){
        this.first_msg_id = first_msg_id;
        this.unique_id = unique_id;
        this.server_salt = server_salt;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        first_msg_id = buffer.readLong();
        unique_id = buffer.readLong();
        server_salt = buffer.readLong();
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
        buff.writeLong(first_msg_id);
        buff.writeLong(unique_id);
        buff.writeLong(server_salt);
    }

    public int getConstructor() {
        return ID;
    }
}