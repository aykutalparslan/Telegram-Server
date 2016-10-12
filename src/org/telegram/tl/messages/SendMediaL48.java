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

package org.telegram.tl.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class SendMediaL48 extends TLObject {

    public static final int ID = 0xc8f16791;

    public int flags;
    public TLInputPeer peer;
    public int reply_to_msg_id;
    public TLInputMedia media;
    public long random_id;
    public TLReplyMarkup reply_markup;

    public SendMediaL48() {
    }

    public SendMediaL48(int flags, TLInputPeer peer, int reply_to_msg_id, TLInputMedia media, long random_id, TLReplyMarkup reply_markup) {
        this.flags = flags;
        this.peer = peer;
        this.reply_to_msg_id = reply_to_msg_id;
        this.media = media;
        this.random_id = random_id;
        this.reply_markup = reply_markup;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        if ((flags & (1 << 0)) != 0) {
            reply_to_msg_id = buffer.readInt();
        }
        media = (TLInputMedia) buffer.readTLObject(APIContext.getInstance());
        random_id = buffer.readLong();
        if ((flags & (1 << 2)) != 0) {
            reply_markup = (TLReplyMarkup) buffer.readTLObject(APIContext.getInstance());
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(32);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (reply_to_msg_id != 0) {
            flags |= (1 << 0);
        }
        if (reply_markup != null) {
            flags |= (1 << 2);
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
        buff.writeTLObject(media);
        buff.writeLong(random_id);
        if ((flags & (1 << 2)) != 0) {
            buff.writeTLObject(reply_markup);
        }
    }

    public boolean is_messages.

    sendMediaL48_silent() {
        return (flags & (1 << 5)) != 0;
    }

    public boolean set_messages.

    sendMediaL48_silent() {
        return (flags |= (1 << 5)) != 0;
    }

    public boolean is_messages.

    sendMediaL48_background() {
        return (flags & (1 << 6)) != 0;
    }

    public boolean set_messages.

    sendMediaL48_background() {
        return (flags |= (1 << 6)) != 0;
    }

    public boolean is_messages.

    sendMediaL48_clear_draft() {
        return (flags & (1 << 7)) != 0;
    }

    public boolean set_messages.

    sendMediaL48_clear_draft() {
        return (flags |= (1 << 7)) != 0;
    }

    public int getConstructor() {
        return ID;
    }
}