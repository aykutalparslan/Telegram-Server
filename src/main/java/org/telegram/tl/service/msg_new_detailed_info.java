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

public class msg_new_detailed_info extends TLObject {

    public static final int ID = 0x809db6df;

    public long answer_msg_id;
    public int bytes;
    public int status;

    public msg_new_detailed_info() {

    }

    public msg_new_detailed_info(long answer_msg_id, int bytes, int status){
        this.answer_msg_id = answer_msg_id;
        this.bytes = bytes;
        this.status = status;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        answer_msg_id = buffer.readLong();
        bytes = buffer.readInt();
        status = buffer.readInt();
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
        buff.writeLong(answer_msg_id);
        buff.writeInt(bytes);
        buff.writeInt(status);
    }

    public int getConstructor() {
        return ID;
    }
}