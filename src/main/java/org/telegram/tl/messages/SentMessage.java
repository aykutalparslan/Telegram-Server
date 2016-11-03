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

import java.util.ArrayList;

public class SentMessage extends TLSentMessage {

    public static final int ID = 0x8a99d8e0;

    public int id;
    public int date;
    public TLMessageMedia media;
    public TLVector<TLMessageEntity> entities = new TLVector<>();
    public int pts;
    public int pts_count;
    public int seq;

    public SentMessage() {
    }

    public SentMessage(int id, int date, TLMessageMedia media, TLVector<TLMessageEntity> entities, int pts, int pts_count, int seq) {
        this.id = id;
        this.date = date;
        this.media = media;
        this.entities = entities;
        this.pts = pts;
        this.pts_count = pts_count;
        this.seq = seq;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readInt();
        date = buffer.readInt();
        media = (TLMessageMedia) buffer.readTLObject(APIContext.getInstance());
        entities = (TLVector<TLMessageEntity>) buffer.readTLObject(APIContext.getInstance());
        pts = buffer.readInt();
        pts_count = buffer.readInt();
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
        buff.writeInt(date);
        buff.writeTLObject(media);
        buff.writeTLObject(entities);
        buff.writeInt(pts);
        buff.writeInt(pts_count);
        buff.writeInt(seq);
    }

    public int getConstructor() {
        return ID;
    }
}