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

package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class GetDocumentByHash extends TLObject {

    public static final int ID = 0x338e2464;

    public byte[] sha256;
    public int size;
    public String mime_type;

    public GetDocumentByHash() {
    }

    public GetDocumentByHash(byte[] sha256, int size, String mime_type) {
        this.sha256 = sha256;
        this.size = size;
        this.mime_type = mime_type;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        sha256 = buffer.readBytes();
        size = buffer.readInt();
        mime_type = buffer.readString();
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
        buff.writeBytes(sha256);
        buff.writeInt(size);
        buff.writeString(mime_type);
    }


    public int getConstructor() {
        return ID;
    }
}