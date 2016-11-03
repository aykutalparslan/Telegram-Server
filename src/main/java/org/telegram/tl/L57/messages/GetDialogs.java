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

package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class GetDialogs extends TLObject {

    public static final int ID = 0x6b47f94d;

    public int offset_date;
    public int offset_id;
    public org.telegram.tl.TLInputPeer offset_peer;
    public int limit;

    public GetDialogs() {
    }

    public GetDialogs(int offset_date, int offset_id, org.telegram.tl.TLInputPeer offset_peer, int limit) {
        this.offset_date = offset_date;
        this.offset_id = offset_id;
        this.offset_peer = offset_peer;
        this.limit = limit;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        offset_date = buffer.readInt();
        offset_id = buffer.readInt();
        offset_peer = (org.telegram.tl.TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        limit = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(24);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(offset_date);
        buff.writeInt(offset_id);
        buff.writeTLObject(offset_peer);
        buff.writeInt(limit);
    }


    public int getConstructor() {
        return ID;
    }
}