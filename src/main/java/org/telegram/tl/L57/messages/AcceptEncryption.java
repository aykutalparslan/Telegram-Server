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

public class AcceptEncryption extends TLObject {

    public static final int ID = 0x3dbc0415;

    public org.telegram.tl.TLInputEncryptedChat peer;
    public byte[] g_b;
    public long key_fingerprint;

    public AcceptEncryption() {
    }

    public AcceptEncryption(org.telegram.tl.TLInputEncryptedChat peer, byte[] g_b, long key_fingerprint) {
        this.peer = peer;
        this.g_b = g_b;
        this.key_fingerprint = key_fingerprint;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (org.telegram.tl.TLInputEncryptedChat) buffer.readTLObject(APIContext.getInstance());
        g_b = buffer.readBytes();
        key_fingerprint = buffer.readLong();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(44);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeTLObject(peer);
        buff.writeBytes(g_b);
        buff.writeLong(key_fingerprint);
    }


    public int getConstructor() {
        return ID;
    }
}