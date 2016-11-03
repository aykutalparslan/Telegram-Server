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

public class NearestDc extends org.telegram.tl.TLNearestDc {

    public static final int ID = 0x8e1a1775;

    public String country;
    public int this_dc;
    public int nearest_dc;

    public NearestDc() {
    }

    public NearestDc(String country, int this_dc, int nearest_dc) {
        this.country = country;
        this.this_dc = this_dc;
        this.nearest_dc = nearest_dc;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        country = buffer.readString();
        this_dc = buffer.readInt();
        nearest_dc = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(20);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeString(country);
        buff.writeInt(this_dc);
        buff.writeInt(nearest_dc);
    }


    public int getConstructor() {
        return ID;
    }
}