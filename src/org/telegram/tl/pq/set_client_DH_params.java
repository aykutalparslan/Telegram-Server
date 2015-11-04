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

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;

/**
 * Created by aykut on 24/10/15.
 */
public class set_client_DH_params extends TLObject{
    public static final int ID = 0xf5045f1f;
    public byte[] nonce;
    public byte[] server_nonce;
    public byte[] encrypted_data;

    public set_client_DH_params(){
    }

    public set_client_DH_params(byte[] nonce, byte[] server_nonce, byte[] encrypted_data){
        if (nonce == null || nonce.length != 16) {
            throw new IllegalArgumentException("must be 16 bytes");
        }
        this.nonce = nonce;
        if (server_nonce == null || server_nonce.length != 16) {
            throw new IllegalArgumentException("must be 16 bytes");
        }
        this.server_nonce = server_nonce;
        this.encrypted_data = encrypted_data;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        nonce = buffer.read(16);
        server_nonce = buffer.read(16);
        encrypted_data = buffer.readBytes();
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
        buff.writeBytes(encrypted_data);

    }

    @Override
    public int getConstructor() {
        return ID;
    }
}
