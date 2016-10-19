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

public class GetBotCallbackAnswer extends TLObject {

    public static final int ID = 0x810a9fec;

    public int flags;
    public org.telegram.tl.TLInputPeer peer;
    public int msg_id;
    public byte[] data;

    public GetBotCallbackAnswer() {
    }

    public GetBotCallbackAnswer(int flags, org.telegram.tl.TLInputPeer peer, int msg_id, byte[] data) {
        this.flags = flags;
        this.peer = peer;
        this.msg_id = msg_id;
        this.data = data;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        peer = (org.telegram.tl.TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        msg_id = buffer.readInt();
        if ((flags & (1 << 0)) != 0) {
            data = buffer.readBytes();
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(36);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (data.length != 0) {
            flags |= (1 << 0);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeTLObject(peer);
        buff.writeInt(msg_id);
        if ((flags & (1 << 0)) != 0) {
            buff.writeBytes(data);
        }
    }

    public boolean is_game() {
        return (flags & (1 << 1)) != 0;
    }

    public void set_game(boolean v) {
        if (v) {
            flags |= (1 << 1);
        } else {
            flags &= ~(1 << 1);
        }
    }

    public int getConstructor() {
        return ID;
    }
}