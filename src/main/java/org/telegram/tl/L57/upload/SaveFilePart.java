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

package org.telegram.tl.L57.upload;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SaveFilePart extends TLObject {

    public static final int ID = 0xb304a621;

    public long file_id;
    public int file_part;
    public byte[] bytes;

    public SaveFilePart() {
    }

    public SaveFilePart(long file_id, int file_part, byte[] bytes) {
        this.file_id = file_id;
        this.file_part = file_part;
        this.bytes = bytes;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        file_id = buffer.readLong();
        file_part = buffer.readInt();
        bytes = buffer.readBytes();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(40);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeLong(file_id);
        buff.writeInt(file_part);
        buff.writeBytes(bytes);
    }


    public int getConstructor() {
        return ID;
    }
}