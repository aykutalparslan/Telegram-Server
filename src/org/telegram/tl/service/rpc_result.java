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

public class rpc_result extends TLObject {

    public static final int ID = 0xf35c6d01;

    public long req_msg_id;
    public TLObject result;

    public rpc_result() {

    }

    public rpc_result(long req_msg_id, TLObject result){
        this.req_msg_id = req_msg_id;
        this.result = result;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        req_msg_id = buffer.readLong();
        result = (TLObject) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(result);
    }

    public int getConstructor() {
        return ID;
    }
}