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

import org.telegram.core.TLContext;
import org.telegram.core.TLMethod;
import org.telegram.data.DatabaseConnection;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;
import org.telegram.tl.service.rpc_error;

public class SaveBigFilePart extends TLObject implements TLMethod {

    public static final int ID = 0xde7b673d;

    public long file_id;
    public int file_part;
    public int file_total_parts;
    public byte[] bytes;

    public SaveBigFilePart() {
    }

    public SaveBigFilePart(long file_id, int file_part, int file_total_parts, byte[] bytes){
        this.file_id = file_id;
        this.file_part = file_part;
        this.file_total_parts = file_total_parts;
        this.bytes = bytes;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        file_id = buffer.readLong();
        file_part = buffer.readInt();
        file_total_parts = buffer.readInt();
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
        buff.writeLong(file_id);
        buff.writeInt(file_part);
        buff.writeInt(file_total_parts);
        buff.writeBytes(bytes);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        if (context.isAuthorized()) {
            DatabaseConnection.getInstance().saveFilePart(file_id, file_part, bytes);
            return new BoolTrue();
        } else {
            return new rpc_error(401, "UNAUTHORIZED");
        }
    }
}