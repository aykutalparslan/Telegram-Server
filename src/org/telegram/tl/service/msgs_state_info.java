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

public class msgs_state_info extends TLObject {

    public static final int ID = 0x04deb57d;

    public long req_msg_id;
    public String info;

    public msgs_state_info() {

    }

    public msgs_state_info(long req_msg_id, String info){
        this.req_msg_id = req_msg_id;
        this.info = info;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        req_msg_id = buffer.readLong();
        info = buffer.readString();
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
        buff.writeLong(req_msg_id);
        buff.writeString(info);
    }

    public int getConstructor() {
        return ID;
    }
}