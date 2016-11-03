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

public class UpdateShortSentMessage extends TLUpdates {

    public static final int ID = 0x11f1331c;

    public int flags;
    public int id;
    public int pts;
    public int pts_count;
    public int date;
    public TLMessageMedia media;
    public TLVector<TLMessageEntity> entities;

    public UpdateShortSentMessage() {
        this.entities = new TLVector<>();
    }

    public UpdateShortSentMessage(int flags, int id, int pts, int pts_count, int date, TLMessageMedia media, TLVector<TLMessageEntity> entities) {
        this.flags = flags;
        this.id = id;
        this.pts = pts;
        this.pts_count = pts_count;
        this.date = date;
        this.media = media;
        this.entities = entities;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = buffer.readInt();
        pts = buffer.readInt();
        pts_count = buffer.readInt();
        date = buffer.readInt();
        if ((flags & (1 << 9)) != 0) {
            media = (TLMessageMedia) buffer.readTLObject(APIContext.getInstance());
        }
        if ((flags & (1 << 7)) != 0) {
            entities = (TLVector<TLMessageEntity>) buffer.readTLVector(TLMessageEntity.class);
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(32);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (media != null) {
            flags |= (1 << 9);
        }
        if (entities != null) {
            flags |= (1 << 7);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeInt(id);
        buff.writeInt(pts);
        buff.writeInt(pts_count);
        buff.writeInt(date);
        if ((flags & (1 << 9)) != 0) {
            buff.writeTLObject(media);
        }
        if ((flags & (1 << 7)) != 0) {
            buff.writeTLObject(entities);
        }
    }

    public boolean is_updateShortSentMessage_out() {
        return (flags & (1 << 1)) != 0;
    }

    public void set_updateShortSentMessage_out() {
        flags |= (1 << 1);
    }

    public int getConstructor() {
        return ID;
    }
}