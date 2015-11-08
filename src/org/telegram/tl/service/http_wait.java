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

package org.telegram.tl.service;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class http_wait extends TLObject {

    public static final int ID = 0x9299359f;

    public int max_delay;
    public int wait_after;
    public int max_wait;

    public http_wait() {

    }

    public http_wait(int max_delay, int wait_after, int max_wait){
        this.max_delay = max_delay;
        this.wait_after = wait_after;
        this.max_wait = max_wait;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        max_delay = buffer.readInt();
        wait_after = buffer.readInt();
        max_wait = buffer.readInt();
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
        buff.writeInt(max_delay);
        buff.writeInt(wait_after);
        buff.writeInt(max_wait);
    }

    public int getConstructor() {
        return ID;
    }
}