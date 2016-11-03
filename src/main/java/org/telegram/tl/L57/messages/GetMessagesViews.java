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

public class GetMessagesViews extends TLObject {

    public static final int ID = 0xc4c8a55d;

    public org.telegram.tl.TLInputPeer peer;
    public TLVector<Integer> id;
    public boolean increment;

    public GetMessagesViews() {
        this.id = new TLVector<>();
    }

    public GetMessagesViews(org.telegram.tl.TLInputPeer peer, TLVector<Integer> id, boolean increment) {
        this.peer = peer;
        this.id = id;
        this.increment = increment;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (org.telegram.tl.TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        id = (TLVector<Integer>) buffer.readTLObject(APIContext.getInstance());
        increment = buffer.readBool();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(21);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeTLObject(peer);
        buff.writeTLObject(id);
        buff.writeBool(increment);
    }


    public int getConstructor() {
        return ID;
    }
}