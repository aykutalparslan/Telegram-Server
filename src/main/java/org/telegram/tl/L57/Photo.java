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

public class Photo extends org.telegram.tl.photos.TLPhoto {

    public static final int ID = 0x9288dd29;

    public int flags;
    public long id;
    public long access_hash;
    public int date;
    public TLVector<org.telegram.tl.TLPhotoSize> sizes;

    public Photo() {
        this.sizes = new TLVector<>();
    }

    public Photo(int flags, long id, long access_hash, int date, TLVector<org.telegram.tl.TLPhotoSize> sizes) {
        this.flags = flags;
        this.id = id;
        this.access_hash = access_hash;
        this.date = date;
        this.sizes = sizes;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = buffer.readLong();
        access_hash = buffer.readLong();
        date = buffer.readInt();
        sizes = (TLVector<org.telegram.tl.TLPhotoSize>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(44);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeLong(id);
        buff.writeLong(access_hash);
        buff.writeInt(date);
        buff.writeTLObject(sizes);
    }

    public boolean is_has_stickers() {
        return (flags & (1 << 0)) != 0;
    }

    public void set_has_stickers(boolean v) {
        if (v) {
            flags |= (1 << 0);
        } else {
            flags &= ~(1 << 0);
        }
    }

    public int getConstructor() {
        return ID;
    }
}