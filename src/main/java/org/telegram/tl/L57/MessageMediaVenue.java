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

public class MessageMediaVenue extends org.telegram.tl.TLMessageMedia {

    public static final int ID = 0x7912b71f;

    public org.telegram.tl.TLGeoPoint geo;
    public String title;
    public String address;
    public String provider;
    public String venue_id;

    public MessageMediaVenue() {
    }

    public MessageMediaVenue(org.telegram.tl.TLGeoPoint geo, String title, String address, String provider, String venue_id) {
        this.geo = geo;
        this.title = title;
        this.address = address;
        this.provider = provider;
        this.venue_id = venue_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        geo = (org.telegram.tl.TLGeoPoint) buffer.readTLObject(APIContext.getInstance());
        title = buffer.readString();
        address = buffer.readString();
        provider = buffer.readString();
        venue_id = buffer.readString();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(44);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeTLObject(geo);
        buff.writeString(title);
        buff.writeString(address);
        buff.writeString(provider);
        buff.writeString(venue_id);
    }


    public int getConstructor() {
        return ID;
    }
}