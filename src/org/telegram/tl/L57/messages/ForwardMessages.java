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

public class ForwardMessages extends TLObject {

    public static final int ID = 0x708e0195;

    public int flags;
    public org.telegram.tl.TLInputPeer from_peer;
    public TLVector<Integer> id;
    public TLVector<Long> random_id;
    public org.telegram.tl.TLInputPeer to_peer;

    public ForwardMessages() {
        this.id = new TLVector<>();
        this.random_id = new TLVector<>();
    }

    public ForwardMessages(int flags, org.telegram.tl.TLInputPeer from_peer, TLVector<Integer> id, TLVector<Long> random_id, org.telegram.tl.TLInputPeer to_peer) {
        this.flags = flags;
        this.from_peer = from_peer;
        this.id = id;
        this.random_id = random_id;
        this.to_peer = to_peer;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        from_peer = (org.telegram.tl.TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        id = (TLVector<Integer>) buffer.readTLObject(APIContext.getInstance());
        random_id = (TLVector<Long>) buffer.readTLObject(APIContext.getInstance());
        to_peer = (org.telegram.tl.TLInputPeer) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(64);
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
        buff.writeTLObject(from_peer);
        buff.writeTLObject(id);
        buff.writeTLObject(random_id);
        buff.writeTLObject(to_peer);
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

    public boolean is_with_my_score() {
        return (flags & (1 << 8)) != 0;
    }

    public void set_with_my_score(boolean v) {
        if (v) {
            flags |= (1 << 8);
        } else {
            flags &= ~(1 << 8);
        }
    }

    public int getConstructor() {
        return ID;
    }
}