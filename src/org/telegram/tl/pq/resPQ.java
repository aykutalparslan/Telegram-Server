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

package org.telegram.tl.pq;

import org.telegram.tl.APIContext;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;

/**
 * Created by aykut on 24/10/15.
 */
public class resPQ extends TLObject{
    public static final int ID = 0x05162463;
    public byte[] nonce;
    public byte[] server_nonce;
    public byte[] pq;
    public TLVector<Long> server_public_key_fingerprints;

    public resPQ(){
        this.server_public_key_fingerprints = new TLVector<>();
    }

    public resPQ(byte[] nonce, byte[] server_nonce, byte[] pq, TLVector<Long> server_public_key_fingerprints){
        if (nonce == null || nonce.length != 16) {
            throw new IllegalArgumentException("must be 16 bytes");
        }
        this.nonce = nonce;
        if (server_nonce == null || server_nonce.length != 16) {
            throw new IllegalArgumentException("must be 16 bytes");
        }
        this.server_nonce = server_nonce;
        this.pq = pq;
        if(server_public_key_fingerprints == null){
            this.server_public_key_fingerprints = new TLVector<>();
        }
        this.server_public_key_fingerprints = server_public_key_fingerprints;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        nonce = buffer.read(16);
        server_nonce = buffer.read(16);
        pq = buffer.readBytes();
        server_public_key_fingerprints = (TLVector<Long>)buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(64);
        serializeTo(buffer);
        return buffer;
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.write(nonce);
        buff.write(server_nonce);
        buff.writeBytes(pq);
        buff.writeTLObject(server_public_key_fingerprints);
    }

    @Override
    public int getConstructor() {
        return ID;
    }
}
