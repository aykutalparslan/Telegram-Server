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

public class UpdateStickerSetsOrder extends TLUpdate {

    public static final int ID = 0xbb2d201;

    public int flags;
    public TLVector<Long> order;

    public UpdateStickerSetsOrder() {
        this.order = new TLVector<>();
    }

    public UpdateStickerSetsOrder(int flags, TLVector<Long> order) {
        this.flags = flags;
        this.order = order;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        order = (TLVector<Long>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(32);
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
        buff.writeTLObject(order);
    }

    public boolean is_updateStickerSetsOrder_masks() {
        return (flags & (1 << 0)) != 0;
    }

    public boolean set_updateStickerSetsOrder_masks() {
        return (flags |= (1 << 0)) != 0;
    }

    public int getConstructor() {
        return ID;
    }
}