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

package org.telegram.tl.L57.updates;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ChannelDifferenceTooLong extends org.telegram.tl.updates.TLChannelDifference {

    public static final int ID = 0x410dee07;

    public int flags;
    public int pts;
    public int timeout;
    public int top_message;
    public int read_inbox_max_id;
    public int read_outbox_max_id;
    public int unread_count;
    public TLVector<org.telegram.tl.TLMessage> messages;
    public TLVector<org.telegram.tl.TLChat> chats;
    public TLVector<org.telegram.tl.TLUser> users;

    public ChannelDifferenceTooLong() {
        this.messages = new TLVector<>();
        this.chats = new TLVector<>();
        this.users = new TLVector<>();
    }

    public ChannelDifferenceTooLong(int flags, int pts, int timeout, int top_message, int read_inbox_max_id, int read_outbox_max_id, int unread_count, TLVector<org.telegram.tl.TLMessage> messages, TLVector<org.telegram.tl.TLChat> chats, TLVector<org.telegram.tl.TLUser> users) {
        this.flags = flags;
        this.pts = pts;
        this.timeout = timeout;
        this.top_message = top_message;
        this.read_inbox_max_id = read_inbox_max_id;
        this.read_outbox_max_id = read_outbox_max_id;
        this.unread_count = unread_count;
        this.messages = messages;
        this.chats = chats;
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        pts = buffer.readInt();
        if ((flags & (1 << 1)) != 0) {
            timeout = buffer.readInt();
        }
        top_message = buffer.readInt();
        read_inbox_max_id = buffer.readInt();
        read_outbox_max_id = buffer.readInt();
        unread_count = buffer.readInt();
        messages = (TLVector<org.telegram.tl.TLMessage>) buffer.readTLObject(APIContext.getInstance());
        chats = (TLVector<org.telegram.tl.TLChat>) buffer.readTLObject(APIContext.getInstance());
        users = (TLVector<org.telegram.tl.TLUser>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(68);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (timeout != 0) {
            flags |= (1 << 1);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeInt(pts);
        if ((flags & (1 << 1)) != 0) {
            buff.writeInt(timeout);
        }
        buff.writeInt(top_message);
        buff.writeInt(read_inbox_max_id);
        buff.writeInt(read_outbox_max_id);
        buff.writeInt(unread_count);
        buff.writeTLObject(messages);
        buff.writeTLObject(chats);
        buff.writeTLObject(users);
    }

    public boolean is_final() {
        return (flags & (1 << 0)) != 0;
    }

    public void set_final(boolean v) {
        if (v) {
            flags |= (1 << 0);
        } else {
            flags &= ~(1 << 0);
        }
    }

    public int getConstructor() {
        return ID;
    }
}