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

package org.telegram.tl.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class ReadHistory extends TLObject {

    public static final int ID = -287800122;

    public TLInputPeer peer;
    public int max_id;
    public int offset;
    public boolean read_contents;

    public ReadHistory(TLInputPeer peer, int max_id, int offset, boolean read_contents){
        this.peer = peer;
        this.max_id = max_id;
        this.offset = offset;
        this.read_contents = read_contents;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        max_id = buffer.readInt();
        offset = buffer.readInt();
        read_contents = buffer.readBool();
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
        buff.writeTLObject(peer);
        buff.writeInt(max_id);
        buff.writeInt(offset);
        buff.writeBool(read_contents);
    }

    public int getConstructor() {
        return ID;
    }
}