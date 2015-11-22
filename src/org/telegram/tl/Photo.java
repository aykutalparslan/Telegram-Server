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

public class Photo extends TLPhoto {

    public static final int ID = 0xcded42fe;

    public long id;
    public long access_hash;
    public int date;
    public TLVector<TLPhotoSize> sizes;

    public Photo() {
        this.sizes = new TLVector<>();
    }

    public Photo(long id, long access_hash, int date, TLVector<TLPhotoSize> sizes) {
        this.id = id;
        this.access_hash = access_hash;
        this.date = date;
        this.sizes = sizes;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readLong();
        access_hash = buffer.readLong();
        date = buffer.readInt();
        sizes = (TLVector<TLPhotoSize>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeLong(id);
        buff.writeLong(access_hash);
        buff.writeInt(date);
        buff.writeTLObject(sizes);
    }

    public int getConstructor() {
        return ID;
    }
}