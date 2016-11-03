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

public class SendInlineBotResult extends TLObject {

    public static final int ID = 0xb16e06fe;

    public int flags;
    public org.telegram.tl.TLInputPeer peer;
    public int reply_to_msg_id;
    public long random_id;
    public long query_id;
    public String id;

    public SendInlineBotResult() {
    }

    public SendInlineBotResult(int flags, org.telegram.tl.TLInputPeer peer, int reply_to_msg_id, long random_id, long query_id, String id) {
        this.flags = flags;
        this.peer = peer;
        this.reply_to_msg_id = reply_to_msg_id;
        this.random_id = random_id;
        this.query_id = query_id;
        this.id = id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        peer = (org.telegram.tl.TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        if ((flags & (1 << 0)) != 0) {
            reply_to_msg_id = buffer.readInt();
        }
        random_id = buffer.readLong();
        query_id = buffer.readLong();
        id = buffer.readString();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(72);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (reply_to_msg_id != 0) {
            flags |= (1 << 0);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeTLObject(peer);
        if ((flags & (1 << 0)) != 0) {
            buff.writeInt(reply_to_msg_id);
        }
        buff.writeLong(random_id);
        buff.writeLong(query_id);
        buff.writeString(id);
    }

    public boolean is_silent() {
        return (flags & (1 << 5)) != 0;
    }

    public void set_silent(boolean v) {
        if (v) {
            flags |= (1 << 5);
        } else {
            flags &= ~(1 << 5);
        }
    }

    public boolean is_background() {
        return (flags & (1 << 6)) != 0;
    }

    public void set_background(boolean v) {
        if (v) {
            flags |= (1 << 6);
        } else {
            flags &= ~(1 << 6);
        }
    }

    public boolean is_clear_draft() {
        return (flags & (1 << 7)) != 0;
    }

    public void set_clear_draft(boolean v) {
        if (v) {
            flags |= (1 << 7);
        } else {
            flags &= ~(1 << 7);
        }
    }

    public int getConstructor() {
        return ID;
    }
}