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

public class InputBotInlineResult extends org.telegram.tl.TLInputBotInlineResult {

    public static final int ID = 0x2cbbe15a;

    public int flags;
    public String id;
    public String type;
    public String title;
    public String description;
    public String url;
    public String thumb_url;
    public String content_url;
    public String content_type;
    public int w;
    public int h;
    public int duration;
    public org.telegram.tl.TLInputBotInlineMessage send_message;

    public InputBotInlineResult() {
    }

    public InputBotInlineResult(int flags, String id, String type, String title, String description, String url, String thumb_url, String content_url, String content_type, int w, int h, int duration, org.telegram.tl.TLInputBotInlineMessage send_message) {
        this.flags = flags;
        this.id = id;
        this.type = type;
        this.title = title;
        this.description = description;
        this.url = url;
        this.thumb_url = thumb_url;
        this.content_url = content_url;
        this.content_type = content_type;
        this.w = w;
        this.h = h;
        this.duration = duration;
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
        if ((flags & (1 << 3)) != 0) {
            url = buffer.readString();
        }
        if ((flags & (1 << 4)) != 0) {
            thumb_url = buffer.readString();
        }
        if ((flags & (1 << 5)) != 0) {
            content_url = buffer.readString();
        }
        if ((flags & (1 << 5)) != 0) {
            content_type = buffer.readString();
        }
        if ((flags & (1 << 6)) != 0) {
            w = buffer.readInt();
        }
        if ((flags & (1 << 6)) != 0) {
            h = buffer.readInt();
        }
        if ((flags & (1 << 7)) != 0) {
            duration = buffer.readInt();
        }
        send_message = (org.telegram.tl.TLInputBotInlineMessage) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(104);
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
        if (url != null && !url.isEmpty()) {
            flags |= (1 << 3);
        }
        if (thumb_url != null && !thumb_url.isEmpty()) {
            flags |= (1 << 4);
        }
        if (content_url != null && !content_url.isEmpty()) {
            flags |= (1 << 5);
        }
        if (content_type != null && !content_type.isEmpty()) {
            flags |= (1 << 5);
        }
        if (w != 0) {
            flags |= (1 << 6);
        }
        if (h != 0) {
            flags |= (1 << 6);
        }
        if (duration != 0) {
            flags |= (1 << 7);
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
        if ((flags & (1 << 3)) != 0) {
            buff.writeString(url);
        }
        if ((flags & (1 << 4)) != 0) {
            buff.writeString(thumb_url);
        }
        if ((flags & (1 << 5)) != 0) {
            buff.writeString(content_url);
        }
        if ((flags & (1 << 5)) != 0) {
            buff.writeString(content_type);
        }
        if ((flags & (1 << 6)) != 0) {
            buff.writeInt(w);
        }
        if ((flags & (1 << 6)) != 0) {
            buff.writeInt(h);
        }
        if ((flags & (1 << 7)) != 0) {
            buff.writeInt(duration);
        }
        buff.writeTLObject(send_message);
    }


    public int getConstructor() {
        return ID;
    }
}