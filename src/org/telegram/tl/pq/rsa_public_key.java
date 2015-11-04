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
public class rsa_public_key extends TLObject{
    public static final int ID = 0x7a19cb76;
    public byte[] n;
    public byte[] e;

    public rsa_public_key(){
    }

    public rsa_public_key(byte[] n, byte[] e){
        this.n = n;
        this.e = e;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        n = buffer.readBytes();
        e = buffer.readBytes();
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
        buff.writeBytes(n);
        buff.writeBytes(e);

    }

    @Override
    public int getConstructor() {
        return ID;
    }
}
