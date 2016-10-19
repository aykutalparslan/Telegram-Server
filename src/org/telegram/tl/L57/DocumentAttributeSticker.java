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

public class DocumentAttributeSticker extends org.telegram.tl.TLDocumentAttribute {

    public static final int ID = 0x6319d612;

    public int flags;
    public String alt;
    public org.telegram.tl.TLInputStickerSet stickerset;
    public org.telegram.tl.TLMaskCoords mask_coords;

    public DocumentAttributeSticker() {
    }

    public DocumentAttributeSticker(int flags, String alt, org.telegram.tl.TLInputStickerSet stickerset, org.telegram.tl.TLMaskCoords mask_coords) {
        this.flags = flags;
        this.alt = alt;
        this.stickerset = stickerset;
        this.mask_coords = mask_coords;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        alt = buffer.readString();
        stickerset = (org.telegram.tl.TLInputStickerSet) buffer.readTLObject(APIContext.getInstance());
        if ((flags & (1 << 0)) != 0) {
            mask_coords = (org.telegram.tl.TLMaskCoords) buffer.readTLObject(APIContext.getInstance());
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(40);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (mask_coords != null) {
            flags |= (1 << 0);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeString(alt);
        buff.writeTLObject(stickerset);
        if ((flags & (1 << 0)) != 0) {
            buff.writeTLObject(mask_coords);
        }
    }

    public boolean is_mask() {
        return (flags & (1 << 1)) != 0;
    }

    public void set_mask(boolean v) {
        if (v) {
            flags |= (1 << 1);
        } else {
            flags &= ~(1 << 1);
        }
    }

    public int getConstructor() {
        return ID;
    }
}