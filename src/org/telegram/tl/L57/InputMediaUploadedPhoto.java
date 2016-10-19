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

public class InputMediaUploadedPhoto extends org.telegram.tl.TLInputMedia {

    public static final int ID = 0x630c9af1;

    public int flags;
    public org.telegram.tl.TLInputFile file;
    public String caption;
    public TLVector<org.telegram.tl.TLInputDocument> stickers;

    public InputMediaUploadedPhoto() {
        this.stickers = new TLVector<>();
    }

    public InputMediaUploadedPhoto(int flags, org.telegram.tl.TLInputFile file, String caption, TLVector<org.telegram.tl.TLInputDocument> stickers) {
        this.flags = flags;
        this.file = file;
        this.caption = caption;
        this.stickers = stickers;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        file = (org.telegram.tl.TLInputFile) buffer.readTLObject(APIContext.getInstance());
        caption = buffer.readString();
        if ((flags & (1 << 0)) != 0) {
            stickers = (TLVector<org.telegram.tl.TLInputDocument>) buffer.readTLVector(org.telegram.tl.TLInputDocument.class);
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(32);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (stickers != null) {
            flags |= (1 << 0);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeTLObject(file);
        buff.writeString(caption);
        if ((flags & (1 << 0)) != 0) {
            buff.writeTLObject(stickers);
        }
    }


    public int getConstructor() {
        return ID;
    }
}