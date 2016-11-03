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

package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputAppEvent extends org.telegram.tl.TLInputAppEvent {

    public static final int ID = 0x770656a8;

    public double time;
    public String type;
    public long peer;
    public String data;

    public InputAppEvent() {
    }

    public InputAppEvent(double time, String type, long peer, String data) {
        this.time = time;
        this.type = type;
        this.peer = peer;
        this.data = data;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        time = buffer.readDouble();
        type = buffer.readString();
        peer = buffer.readLong();
        data = buffer.readString();
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
        buff.writeDouble(time);
        buff.writeString(type);
        buff.writeLong(peer);
        buff.writeString(data);
    }


    public int getConstructor() {
        return ID;
    }
}