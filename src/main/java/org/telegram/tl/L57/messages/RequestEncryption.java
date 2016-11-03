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

public class RequestEncryption extends TLObject {

    public static final int ID = 0xf64daf43;

    public org.telegram.tl.TLInputUser user_id;
    public int random_id;
    public byte[] g_a;

    public RequestEncryption() {
    }

    public RequestEncryption(org.telegram.tl.TLInputUser user_id, int random_id, byte[] g_a) {
        this.user_id = user_id;
        this.random_id = random_id;
        this.g_a = g_a;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        user_id = (org.telegram.tl.TLInputUser) buffer.readTLObject(APIContext.getInstance());
        random_id = buffer.readInt();
        g_a = buffer.readBytes();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(40);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeTLObject(user_id);
        buff.writeInt(random_id);
        buff.writeBytes(g_a);
    }


    public int getConstructor() {
        return ID;
    }
}