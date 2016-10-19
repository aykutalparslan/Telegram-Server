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

public class StartBot extends TLObject {

    public static final int ID = 0xe6df7378;

    public org.telegram.tl.TLInputUser bot;
    public org.telegram.tl.TLInputPeer peer;
    public long random_id;
    public String start_param;

    public StartBot() {
    }

    public StartBot(org.telegram.tl.TLInputUser bot, org.telegram.tl.TLInputPeer peer, long random_id, String start_param) {
        this.bot = bot;
        this.peer = peer;
        this.random_id = random_id;
        this.start_param = start_param;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        bot = (org.telegram.tl.TLInputUser) buffer.readTLObject(APIContext.getInstance());
        peer = (org.telegram.tl.TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        random_id = buffer.readLong();
        start_param = buffer.readString();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(36);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeTLObject(bot);
        buff.writeTLObject(peer);
        buff.writeLong(random_id);
        buff.writeString(start_param);
    }


    public int getConstructor() {
        return ID;
    }
}