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
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class MessageServiceL45 extends TLMessage {

    public static final int ID = 0xc06b9607;

    public int flags;
    public int id;
    public int from_id;
    public TLPeer to_id;
    public int date;
    public TLMessageAction action;

    public MessageServiceL45() {
    }

    public MessageServiceL45(int flags, int id, int from_id, TLPeer to_id, int date, TLMessageAction action) {
        this.flags = flags;
        this.id = id;
        this.from_id = from_id;
        this.to_id = to_id;
        this.date = date;
        this.action = action;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = buffer.readInt();
        if ((flags & (1 << 8)) != 0) {
            from_id = buffer.readInt();
        }
        to_id = (TLPeer) buffer.readTLObject(APIContext.getInstance());
        date = buffer.readInt();
        action = (TLMessageAction) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(72);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (from_id != 0) {
            flags |= (1 << 8);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeInt(id);
        if ((flags & (1 << 8)) != 0) {
            buff.writeInt(from_id);
        }
        buff.writeTLObject(to_id);
        buff.writeInt(date);
        buff.writeTLObject(action);
    }

    public boolean is_unread() {
        return (flags & (1 << 0)) != 0;
    }

    public void set_unread(boolean v) {
        if (v) {
            flags |= (1 << 0);
        } else {
            flags &= ~(1 << 0);
        }
    }

    public boolean is_out() {
        return (flags & (1 << 1)) != 0;
    }

    public void set_out(boolean v) {
        if (v) {
            flags |= (1 << 1);
        } else {
            flags &= ~(1 << 1);
        }
    }

    public boolean is_mentioned() {
        return (flags & (1 << 4)) != 0;
    }

    public void set_mentioned(boolean v) {
        if (v) {
            flags |= (1 << 4);
        } else {
            flags &= ~(1 << 4);
        }
    }

    public boolean is_media_unread() {
        return (flags & (1 << 5)) != 0;
    }

    public void set_media_unread(boolean v) {
        if (v) {
            flags |= (1 << 5);
        } else {
            flags &= ~(1 << 5);
        }
    }

    public int getConstructor() {
        return ID;
    }
}