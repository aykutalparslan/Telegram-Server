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

public class InputBotInlineMessageMediaVenue extends org.telegram.tl.TLInputBotInlineMessage {

    public static final int ID = 0xaaafadc8;

    public int flags;
    public org.telegram.tl.TLInputGeoPoint geo_point;
    public String title;
    public String address;
    public String provider;
    public String venue_id;
    public org.telegram.tl.TLReplyMarkup reply_markup;

    public InputBotInlineMessageMediaVenue() {
    }

    public InputBotInlineMessageMediaVenue(int flags, org.telegram.tl.TLInputGeoPoint geo_point, String title, String address, String provider, String venue_id, org.telegram.tl.TLReplyMarkup reply_markup) {
        this.flags = flags;
        this.geo_point = geo_point;
        this.title = title;
        this.address = address;
        this.provider = provider;
        this.venue_id = venue_id;
        this.reply_markup = reply_markup;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        geo_point = (org.telegram.tl.TLInputGeoPoint) buffer.readTLObject(APIContext.getInstance());
        title = buffer.readString();
        address = buffer.readString();
        provider = buffer.readString();
        venue_id = buffer.readString();
        if ((flags & (1 << 2)) != 0) {
            reply_markup = (org.telegram.tl.TLReplyMarkup) buffer.readTLObject(APIContext.getInstance());
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(56);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (reply_markup != null) {
            flags |= (1 << 2);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeTLObject(geo_point);
        buff.writeString(title);
        buff.writeString(address);
        buff.writeString(provider);
        buff.writeString(venue_id);
        if ((flags & (1 << 2)) != 0) {
            buff.writeTLObject(reply_markup);
        }
    }


    public int getConstructor() {
        return ID;
    }
}