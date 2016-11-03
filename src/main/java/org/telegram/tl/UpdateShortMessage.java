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

import java.io.Serializable;

public class UpdateShortMessage extends TLUpdates {

    public static final int ID = 0x3f32d858;

    public int flags;
    public int id;
    public int user_id;
    public String message;
    public int pts;
    public int pts_count;
    public int date;
    public int fwd_from_id;
    public int fwd_date;
    public int reply_to_msg_id;
    public TLVector<TLMessageEntity> entities = new TLVector<>();


    public UpdateShortMessage() {
    }

    public UpdateShortMessage(int flags, int id, int user_id, String message, int pts, int pts_count,
                              int date, int fwd_from_id, int fwd_date, int reply_to_msg_id,
                              TLVector<TLMessageEntity> entities) {
        this.flags = flags;
        this.id = id;
        this.user_id = user_id;
        this.message = message;
        this.pts = pts;
        this.pts_count = pts_count;
        this.date = date;
        this.fwd_from_id = fwd_from_id;
        this.fwd_date = fwd_date;
        this.reply_to_msg_id = reply_to_msg_id;
        this.entities = entities;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = buffer.readInt();
        user_id = buffer.readInt();
        message = buffer.readString();
        pts = buffer.readInt();
        pts_count = buffer.readInt();
        date = buffer.readInt();
        if ((flags & 4) != 0) {
            fwd_from_id = buffer.readInt();
        }
        if ((flags & 4) != 0) {
            fwd_date = buffer.readInt();
        }
        if ((flags & 8) != 0) {
            reply_to_msg_id = buffer.readInt();
        }
        if ((flags & 128) != 0) {
            entities = (TLVector<TLMessageEntity>) buffer.readTLObject(APIContext.getInstance());
        }
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
        buff.writeInt(flags);
        buff.writeInt(id);
        buff.writeInt(user_id);
        buff.writeString(message);
        buff.writeInt(pts);
        buff.writeInt(pts_count);
        buff.writeInt(date);
        if ((flags & 4) != 0) {
            buff.writeInt(fwd_from_id);
        }
        if ((flags & 4) != 0) {
            buff.writeInt(fwd_date);
        }
        if ((flags & 8) != 0) {
            buff.writeInt(reply_to_msg_id);
        }
        if ((flags & 128) != 0) {
            buff.writeTLObject(entities);
        }
    }

    public int getConstructor() {
        return ID;
    }
}