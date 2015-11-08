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

package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class UpdateShortMessage extends TLUpdates {

    public static final int ID = -738961532;

    public int id;
    public int from_id;
    public String message;
    public int pts;
    public int date;
    public int seq;

    public UpdateShortMessage() {
    }

    public UpdateShortMessage(int id, int from_id, String message, int pts, int date, int seq){
        this.id = id;
        this.from_id = from_id;
        this.message = message;
        this.pts = pts;
        this.date = date;
        this.seq = seq;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readInt();
        from_id = buffer.readInt();
        message = buffer.readString();
        pts = buffer.readInt();
        date = buffer.readInt();
        seq = buffer.readInt();
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
        buff.writeInt(id);
        buff.writeInt(from_id);
        buff.writeString(message);
        buff.writeInt(pts);
        buff.writeInt(date);
        buff.writeInt(seq);
    }

    public int getConstructor() {
        return ID;
    }
}