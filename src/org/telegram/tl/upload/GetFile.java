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

package org.telegram.tl.upload;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class GetFile extends TLObject {

    public static final int ID = -475607115;

    public TLInputFileLocation location;
    public int offset;
    public int limit;

    public GetFile(TLInputFileLocation location, int offset, int limit){
        this.location = location;
        this.offset = offset;
        this.limit = limit;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        location = (TLInputFileLocation) buffer.readTLObject(APIContext.getInstance());
        offset = buffer.readInt();
        limit = buffer.readInt();
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
        buff.writeTLObject(location);
        buff.writeInt(offset);
        buff.writeInt(limit);
    }

    public int getConstructor() {
        return ID;
    }
}