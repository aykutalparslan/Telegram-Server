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

public class DhConfig extends org.telegram.tl.messages.TLDhConfig {

    public static final int ID = 0x2c221edd;

    public int g;
    public byte[] p;
    public int version;
    public byte[] random;

    public DhConfig() {
    }

    public DhConfig(int g, byte[] p, int version, byte[] random) {
        this.g = g;
        this.p = p;
        this.version = version;
        this.random = random;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        g = buffer.readInt();
        p = buffer.readBytes();
        version = buffer.readInt();
        random = buffer.readBytes();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(60);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(g);
        buff.writeBytes(p);
        buff.writeInt(version);
        buff.writeBytes(random);
    }


    public int getConstructor() {
        return ID;
    }
}