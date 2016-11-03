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

public class WebPage extends org.telegram.tl.TLWebPage {

    public static final int ID = 0xca820ed7;

    public int flags;
    public long id;
    public String url;
    public String display_url;
    public String type;
    public String site_name;
    public String title;
    public String description;
    public org.telegram.tl.photos.TLPhoto photo;
    public String embed_url;
    public String embed_type;
    public int embed_width;
    public int embed_height;
    public int duration;
    public String author;
    public org.telegram.tl.TLDocument document;

    public WebPage() {
    }

    public WebPage(int flags, long id, String url, String display_url, String type, String site_name, String title, String description, org.telegram.tl.photos.TLPhoto photo, String embed_url, String embed_type, int embed_width, int embed_height, int duration, String author, org.telegram.tl.TLDocument document) {
        this.flags = flags;
        this.id = id;
        this.url = url;
        this.display_url = display_url;
        this.type = type;
        this.site_name = site_name;
        this.title = title;
        this.description = description;
        this.photo = photo;
        this.embed_url = embed_url;
        this.embed_type = embed_type;
        this.embed_width = embed_width;
        this.embed_height = embed_height;
        this.duration = duration;
        this.author = author;
        this.document = document;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = buffer.readLong();
        url = buffer.readString();
        display_url = buffer.readString();
        if ((flags & (1 << 0)) != 0) {
            type = buffer.readString();
        }
        if ((flags & (1 << 1)) != 0) {
            site_name = buffer.readString();
        }
        if ((flags & (1 << 2)) != 0) {
            title = buffer.readString();
        }
        if ((flags & (1 << 3)) != 0) {
            description = buffer.readString();
        }
        if ((flags & (1 << 4)) != 0) {
            photo = (org.telegram.tl.photos.TLPhoto) buffer.readTLObject(APIContext.getInstance());
        }
        if ((flags & (1 << 5)) != 0) {
            embed_url = buffer.readString();
        }
        if ((flags & (1 << 5)) != 0) {
            embed_type = buffer.readString();
        }
        if ((flags & (1 << 6)) != 0) {
            embed_width = buffer.readInt();
        }
        if ((flags & (1 << 6)) != 0) {
            embed_height = buffer.readInt();
        }
        if ((flags & (1 << 7)) != 0) {
            duration = buffer.readInt();
        }
        if ((flags & (1 << 8)) != 0) {
            author = buffer.readString();
        }
        if ((flags & (1 << 9)) != 0) {
            document = (org.telegram.tl.TLDocument) buffer.readTLObject(APIContext.getInstance());
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(128);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (type != null && !type.isEmpty()) {
            flags |= (1 << 0);
        }
        if (site_name != null && !site_name.isEmpty()) {
            flags |= (1 << 1);
        }
        if (title != null && !title.isEmpty()) {
            flags |= (1 << 2);
        }
        if (description != null && !description.isEmpty()) {
            flags |= (1 << 3);
        }
        if (photo != null) {
            flags |= (1 << 4);
        }
        if (embed_url != null && !embed_url.isEmpty()) {
            flags |= (1 << 5);
        }
        if (embed_type != null && !embed_type.isEmpty()) {
            flags |= (1 << 5);
        }
        if (embed_width != 0) {
            flags |= (1 << 6);
        }
        if (embed_height != 0) {
            flags |= (1 << 6);
        }
        if (duration != 0) {
            flags |= (1 << 7);
        }
        if (author != null && !author.isEmpty()) {
            flags |= (1 << 8);
        }
        if (document != null) {
            flags |= (1 << 9);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeLong(id);
        buff.writeString(url);
        buff.writeString(display_url);
        if ((flags & (1 << 0)) != 0) {
            buff.writeString(type);
        }
        if ((flags & (1 << 1)) != 0) {
            buff.writeString(site_name);
        }
        if ((flags & (1 << 2)) != 0) {
            buff.writeString(title);
        }
        if ((flags & (1 << 3)) != 0) {
            buff.writeString(description);
        }
        if ((flags & (1 << 4)) != 0) {
            buff.writeTLObject(photo);
        }
        if ((flags & (1 << 5)) != 0) {
            buff.writeString(embed_url);
        }
        if ((flags & (1 << 5)) != 0) {
            buff.writeString(embed_type);
        }
        if ((flags & (1 << 6)) != 0) {
            buff.writeInt(embed_width);
        }
        if ((flags & (1 << 6)) != 0) {
            buff.writeInt(embed_height);
        }
        if ((flags & (1 << 7)) != 0) {
            buff.writeInt(duration);
        }
        if ((flags & (1 << 8)) != 0) {
            buff.writeString(author);
        }
        if ((flags & (1 << 9)) != 0) {
            buff.writeTLObject(document);
        }
    }


    public int getConstructor() {
        return ID;
    }
}