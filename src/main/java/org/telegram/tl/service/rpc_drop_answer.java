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

import org.telegram.core.TLContext;
import org.telegram.core.TLMethod;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class rpc_drop_answer extends TLObject implements TLMethod{

    public static final int ID = 0x58e4a740;

    public long req_msg_id;

    public rpc_drop_answer() {

    }

    public rpc_drop_answer(long req_msg_id){
        this.req_msg_id = req_msg_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        req_msg_id = buffer.readLong();
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
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context,long messageId, long reqMessageId) {
        return new rpc_answer_unknown();
    }
}