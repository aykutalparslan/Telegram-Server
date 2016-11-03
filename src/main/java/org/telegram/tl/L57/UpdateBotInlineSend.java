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

public class UpdateBotInlineSend extends org.telegram.tl.TLUpdate {

    public static final int ID = 0xe48f964;

    public int flags;
    public int user_id;
    public String query;
    public org.telegram.tl.TLGeoPoint geo;
    public String id;
    public org.telegram.tl.TLInputBotInlineMessageID msg_id;

    public UpdateBotInlineSend() {
    }

    public UpdateBotInlineSend(int flags, int user_id, String query, org.telegram.tl.TLGeoPoint geo, String id, org.telegram.tl.TLInputBotInlineMessageID msg_id) {
        this.flags = flags;
        this.user_id = user_id;
        this.query = query;
        this.geo = geo;
        this.id = id;
        this.msg_id = msg_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        user_id = buffer.readInt();
        query = buffer.readString();
        if ((flags & (1 << 0)) != 0) {
            geo = (org.telegram.tl.TLGeoPoint) buffer.readTLObject(APIContext.getInstance());
        }
        id = buffer.readString();
        if ((flags & (1 << 1)) != 0) {
            msg_id = (org.telegram.tl.TLInputBotInlineMessageID) buffer.readTLObject(APIContext.getInstance());
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(44);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (geo != null) {
            flags |= (1 << 0);
        }
        if (msg_id != null) {
            flags |= (1 << 1);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeInt(user_id);
        buff.writeString(query);
        if ((flags & (1 << 0)) != 0) {
            buff.writeTLObject(geo);
        }
        buff.writeString(id);
        if ((flags & (1 << 1)) != 0) {
            buff.writeTLObject(msg_id);
        }
    }


    public int getConstructor() {
        return ID;
    }
}