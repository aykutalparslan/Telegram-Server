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

public class InputMediaUploadedThumbDocument extends TLInputMedia {

    public static final int ID = 1095242886;

    public TLInputFile file;
    public TLInputFile thumb;
    public String mime_type;
    public TLVector<TLDocumentAttribute> attributes;

    public InputMediaUploadedThumbDocument(TLInputFile file, TLInputFile thumb, String mime_type, TLVector<TLDocumentAttribute> attributes){
        this.file = file;
        this.thumb = thumb;
        this.mime_type = mime_type;
        this.attributes = attributes;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        file = (TLInputFile) buffer.readTLObject(APIContext.getInstance());
        thumb = (TLInputFile) buffer.readTLObject(APIContext.getInstance());
        mime_type = buffer.readString();
        attributes = (TLVector<TLDocumentAttribute>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(file);
        buff.writeTLObject(thumb);
        buff.writeString(mime_type);
        buff.writeTLObject(attributes);
    }

    public int getConstructor() {
        return ID;
    }
}