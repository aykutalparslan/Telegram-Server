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

package org.telegram.tl.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;
import org.telegram.tl.contacts.*;

public class SentMessageLink extends TLSentMessage {

    public static final int ID = -371504577;

    public int id;
    public int date;
    public int pts;
    public int seq;
    public TLVector<TLLink> links;

    public SentMessageLink() {
        this.links = new TLVector<>();
    }

    public SentMessageLink(int id, int date, int pts, int seq, TLVector<TLLink> links){
        this.id = id;
        this.date = date;
        this.pts = pts;
        this.seq = seq;
        this.links = links;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readInt();
        date = buffer.readInt();
        pts = buffer.readInt();
        seq = buffer.readInt();
        links = (TLVector<TLLink>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeInt(date);
        buff.writeInt(pts);
        buff.writeInt(seq);
        buff.writeTLObject(links);
    }

    public int getConstructor() {
        return ID;
    }
}