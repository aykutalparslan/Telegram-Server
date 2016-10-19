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

public class SearchGlobal extends TLObject {

    public static final int ID = 0x9e3cacb0;

    public String q;
    public int offset_date;
    public org.telegram.tl.TLInputPeer offset_peer;
    public int offset_id;
    public int limit;

    public SearchGlobal() {
    }

    public SearchGlobal(String q, int offset_date, org.telegram.tl.TLInputPeer offset_peer, int offset_id, int limit) {
        this.q = q;
        this.offset_date = offset_date;
        this.offset_peer = offset_peer;
        this.offset_id = offset_id;
        this.limit = limit;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        q = buffer.readString();
        offset_date = buffer.readInt();
        offset_peer = (org.telegram.tl.TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        offset_id = buffer.readInt();
        limit = buffer.readInt();
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
        buff.writeString(q);
        buff.writeInt(offset_date);
        buff.writeTLObject(offset_peer);
        buff.writeInt(offset_id);
        buff.writeInt(limit);
    }


    public int getConstructor() {
        return ID;
    }
}