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

public class StickerSet extends org.telegram.tl.TLStickerSet {

    public static final int ID = 0xcd303b41;

    public int flags;
    public long id;
    public long access_hash;
    public String title;
    public String short_name;
    public int count;
    public int hash;

    public StickerSet() {
    }

    public StickerSet(int flags, long id, long access_hash, String title, String short_name, int count, int hash) {
        this.flags = flags;
        this.id = id;
        this.access_hash = access_hash;
        this.title = title;
        this.short_name = short_name;
        this.count = count;
        this.hash = hash;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = buffer.readLong();
        access_hash = buffer.readLong();
        title = buffer.readString();
        short_name = buffer.readString();
        count = buffer.readInt();
        hash = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(80);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeLong(id);
        buff.writeLong(access_hash);
        buff.writeString(title);
        buff.writeString(short_name);
        buff.writeInt(count);
        buff.writeInt(hash);
    }

    public boolean is_installed() {
        return (flags & (1 << 0)) != 0;
    }

    public void set_installed(boolean v) {
        if (v) {
            flags |= (1 << 0);
        } else {
            flags &= ~(1 << 0);
        }
    }

    public boolean is_archived() {
        return (flags & (1 << 1)) != 0;
    }

    public void set_archived(boolean v) {
        if (v) {
            flags |= (1 << 1);
        } else {
            flags &= ~(1 << 1);
        }
    }

    public boolean is_official() {
        return (flags & (1 << 2)) != 0;
    }

    public void set_official(boolean v) {
        if (v) {
            flags |= (1 << 2);
        } else {
            flags &= ~(1 << 2);
        }
    }

    public boolean is_masks() {
        return (flags & (1 << 3)) != 0;
    }

    public void set_masks(boolean v) {
        if (v) {
            flags |= (1 << 3);
        } else {
            flags &= ~(1 << 3);
        }
    }

    public int getConstructor() {
        return ID;
    }
}