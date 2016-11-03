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

public class BotInlineMediaResult extends org.telegram.tl.TLBotInlineResult {

    public static final int ID = 0x17db940b;

    public int flags;
    public String id;
    public String type;
    public org.telegram.tl.photos.TLPhoto photo;
    public org.telegram.tl.TLDocument document;
    public String title;
    public String description;
    public org.telegram.tl.TLBotInlineMessage send_message;

    public BotInlineMediaResult() {
    }

    public BotInlineMediaResult(int flags, String id, String type, org.telegram.tl.photos.TLPhoto photo, org.telegram.tl.TLDocument document, String title, String description, org.telegram.tl.TLBotInlineMessage send_message) {
        this.flags = flags;
        this.id = id;
        this.type = type;
        this.photo = photo;
        this.document = document;
        this.title = title;
        this.description = description;
        this.send_message = send_message;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = buffer.readString();
        type = buffer.readString();
        if ((flags & (1 << 0)) != 0) {
            photo = (org.telegram.tl.photos.TLPhoto) buffer.readTLObject(APIContext.getInstance());
        }
        if ((flags & (1 << 1)) != 0) {
            document = (org.telegram.tl.TLDocument) buffer.readTLObject(APIContext.getInstance());
        }
        if ((flags & (1 << 2)) != 0) {
            title = buffer.readString();
        }
        if ((flags & (1 << 3)) != 0) {
            description = buffer.readString();
        }
        send_message = (org.telegram.tl.TLBotInlineMessage) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(64);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (photo != null) {
            flags |= (1 << 0);
        }
        if (document != null) {
            flags |= (1 << 1);
        }
        if (title != null && !title.isEmpty()) {
            flags |= (1 << 2);
        }
        if (description != null && !description.isEmpty()) {
            flags |= (1 << 3);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeString(id);
        buff.writeString(type);
        if ((flags & (1 << 0)) != 0) {
            buff.writeTLObject(photo);
        }
        if ((flags & (1 << 1)) != 0) {
            buff.writeTLObject(document);
        }
        if ((flags & (1 << 2)) != 0) {
            buff.writeString(title);
        }
        if ((flags & (1 << 3)) != 0) {
            buff.writeString(description);
        }
        buff.writeTLObject(send_message);
    }


    public int getConstructor() {
        return ID;
    }
}