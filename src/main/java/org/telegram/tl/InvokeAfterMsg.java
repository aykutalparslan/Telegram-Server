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
import org.telegram.tl.service.message;

public class InvokeAfterMsg extends TLObject {

    public static final int ID = -878758099;

    public long msg_id;
    public TLObject query;

    public InvokeAfterMsg() {
    }

    public InvokeAfterMsg(long msg_id, TLObject query){
        this.msg_id = msg_id;
        this.query = query;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        msg_id = buffer.readLong();
        //query = (TLObject) buffer.readTLObject(APIContext.getInstance());
        query = buffer.readBareTLType(new message());
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
        buff.writeLong(msg_id);
        buff.writeTLObject(query);
    }

    public int getConstructor() {
        return ID;
    }
}