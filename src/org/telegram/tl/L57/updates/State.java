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

package org.telegram.tl.L57.updates;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class State extends org.telegram.tl.updates.TLState {

    public static final int ID = 0xa56c2a3e;

    public int pts;
    public int qts;
    public int date;
    public int seq;
    public int unread_count;

    public State() {
    }

    public State(int pts, int qts, int date, int seq, int unread_count) {
        this.pts = pts;
        this.qts = qts;
        this.date = date;
        this.seq = seq;
        this.unread_count = unread_count;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        pts = buffer.readInt();
        qts = buffer.readInt();
        date = buffer.readInt();
        seq = buffer.readInt();
        unread_count = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(24);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(pts);
        buff.writeInt(qts);
        buff.writeInt(date);
        buff.writeInt(seq);
        buff.writeInt(unread_count);
    }


    public int getConstructor() {
        return ID;
    }
}