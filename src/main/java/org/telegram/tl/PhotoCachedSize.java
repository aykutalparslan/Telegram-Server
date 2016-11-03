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

public class PhotoCachedSize extends TLPhotoSize {

    public static final int ID = -374917894;

    public String type;
    public TLFileLocation location;
    public int w;
    public int h;
    public byte[] bytes;

    public PhotoCachedSize() {
    }

    public PhotoCachedSize(String type, TLFileLocation location, int w, int h, byte[] bytes){
        this.type = type;
        this.location = location;
        this.w = w;
        this.h = h;
        this.bytes = bytes;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        type = buffer.readString();
        location = (TLFileLocation) buffer.readTLObject(APIContext.getInstance());
        w = buffer.readInt();
        h = buffer.readInt();
        bytes = buffer.readBytes();
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
        buff.writeString(type);
        buff.writeTLObject(location);
        buff.writeInt(w);
        buff.writeInt(h);
        buff.writeBytes(bytes);
    }

    public int getConstructor() {
        return ID;
    }
}