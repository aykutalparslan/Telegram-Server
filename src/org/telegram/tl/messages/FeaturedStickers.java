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

public class FeaturedStickers extends TLFeaturedStickers {

    public static final int ID = 0xf89d88e5;

    public int hash;
    public TLVector<TLStickerSetCovered> sets;
    public TLVector<Long> unread;

    public FeaturedStickers() {
        this.sets = new TLVector<>();
        this.unread = new TLVector<>();
    }

    public FeaturedStickers(int hash, TLVector<TLStickerSetCovered> sets, TLVector<Long> unread) {
        this.hash = hash;
        this.sets = sets;
        this.unread = unread;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        hash = buffer.readInt();
        sets = (TLVector<TLStickerSetCovered>) buffer.readTLObject(APIContext.getInstance());
        unread = (TLVector<Long>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeInt(hash);
        buff.writeTLObject(sets);
        buff.writeTLObject(unread);
    }


    public int getConstructor() {
        return ID;
    }
}