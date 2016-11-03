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

public class Game extends org.telegram.tl.TLGame {

    public static final int ID = 0xbdf9653b;

    public int flags;
    public long id;
    public long access_hash;
    public String short_name;
    public String title;
    public String description;
    public org.telegram.tl.photos.TLPhoto photo;
    public org.telegram.tl.TLDocument document;

    public Game() {
    }

    public Game(int flags, long id, long access_hash, String short_name, String title, String description, org.telegram.tl.photos.TLPhoto photo, org.telegram.tl.TLDocument document) {
        this.flags = flags;
        this.id = id;
        this.access_hash = access_hash;
        this.short_name = short_name;
        this.title = title;
        this.description = description;
        this.photo = photo;
        this.document = document;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = buffer.readLong();
        access_hash = buffer.readLong();
        short_name = buffer.readString();
        title = buffer.readString();
        description = buffer.readString();
        photo = (org.telegram.tl.photos.TLPhoto) buffer.readTLObject(APIContext.getInstance());
        if ((flags & (1 << 0)) != 0) {
            document = (org.telegram.tl.TLDocument) buffer.readTLObject(APIContext.getInstance());
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(64);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (document != null) {
            flags |= (1 << 0);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeLong(id);
        buff.writeLong(access_hash);
        buff.writeString(short_name);
        buff.writeString(title);
        buff.writeString(description);
        buff.writeTLObject(photo);
        if ((flags & (1 << 0)) != 0) {
            buff.writeTLObject(document);
        }
    }


    public int getConstructor() {
        return ID;
    }
}