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

public class InputBotInlineResultDocument extends org.telegram.tl.TLInputBotInlineResult {

    public static final int ID = 0xfff8fdc4;

    public int flags;
    public String id;
    public String type;
    public String title;
    public String description;
    public org.telegram.tl.TLInputDocument document;
    public org.telegram.tl.TLInputBotInlineMessage send_message;

    public InputBotInlineResultDocument() {
    }

    public InputBotInlineResultDocument(int flags, String id, String type, String title, String description, org.telegram.tl.TLInputDocument document, org.telegram.tl.TLInputBotInlineMessage send_message) {
        this.flags = flags;
        this.id = id;
        this.type = type;
        this.title = title;
        this.description = description;
        this.document = document;
        this.send_message = send_message;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = buffer.readString();
        type = buffer.readString();
        if ((flags & (1 << 1)) != 0) {
            title = buffer.readString();
        }
        if ((flags & (1 << 2)) != 0) {
            description = buffer.readString();
        }
        document = (org.telegram.tl.TLInputDocument) buffer.readTLObject(APIContext.getInstance());
        send_message = (org.telegram.tl.TLInputBotInlineMessage) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(56);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (title != null && !title.isEmpty()) {
            flags |= (1 << 1);
        }
        if (description != null && !description.isEmpty()) {
            flags |= (1 << 2);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeString(id);
        buff.writeString(type);
        if ((flags & (1 << 1)) != 0) {
            buff.writeString(title);
        }
        if ((flags & (1 << 2)) != 0) {
            buff.writeString(description);
        }
        buff.writeTLObject(document);
        buff.writeTLObject(send_message);
    }


    public int getConstructor() {
        return ID;
    }
}