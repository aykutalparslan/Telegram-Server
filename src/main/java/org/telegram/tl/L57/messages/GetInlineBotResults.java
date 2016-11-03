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

public class GetInlineBotResults extends TLObject {

    public static final int ID = 0x514e999d;

    public int flags;
    public org.telegram.tl.TLInputUser bot;
    public org.telegram.tl.TLInputPeer peer;
    public org.telegram.tl.TLInputGeoPoint geo_point;
    public String query;
    public String offset;

    public GetInlineBotResults() {
    }

    public GetInlineBotResults(int flags, org.telegram.tl.TLInputUser bot, org.telegram.tl.TLInputPeer peer, org.telegram.tl.TLInputGeoPoint geo_point, String query, String offset) {
        this.flags = flags;
        this.bot = bot;
        this.peer = peer;
        this.geo_point = geo_point;
        this.query = query;
        this.offset = offset;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        bot = (org.telegram.tl.TLInputUser) buffer.readTLObject(APIContext.getInstance());
        peer = (org.telegram.tl.TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        if ((flags & (1 << 0)) != 0) {
            geo_point = (org.telegram.tl.TLInputGeoPoint) buffer.readTLObject(APIContext.getInstance());
        }
        query = buffer.readString();
        offset = buffer.readString();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(48);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (geo_point != null) {
            flags |= (1 << 0);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeTLObject(bot);
        buff.writeTLObject(peer);
        if ((flags & (1 << 0)) != 0) {
            buff.writeTLObject(geo_point);
        }
        buff.writeString(query);
        buff.writeString(offset);
    }


    public int getConstructor() {
        return ID;
    }
}