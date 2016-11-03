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

package org.telegram.tl.L57.contacts;

import org.telegram.core.TLContext;
import org.telegram.core.TLMethod;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;
import org.telegram.tl.L57.*;

public class GetTopPeers extends TLObject implements TLMethod {

    public static final int ID = 0xd4982db5;

    public int flags;
    public int offset;
    public int limit;
    public int hash;

    public GetTopPeers() {
    }

    public GetTopPeers(int flags, int offset, int limit, int hash) {
        this.flags = flags;
        this.offset = offset;
        this.limit = limit;
        this.hash = hash;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        offset = buffer.readInt();
        limit = buffer.readInt();
        hash = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(60);
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
        buff.writeInt(offset);
        buff.writeInt(limit);
        buff.writeInt(hash);
    }

    public boolean is_correspondents() {
        return (flags & (1 << 0)) != 0;
    }

    public void set_correspondents(boolean v) {
        if (v) {
            flags |= (1 << 0);
        } else {
            flags &= ~(1 << 0);
        }
    }

    public boolean is_bots_pm() {
        return (flags & (1 << 1)) != 0;
    }

    public void set_bots_pm(boolean v) {
        if (v) {
            flags |= (1 << 1);
        } else {
            flags &= ~(1 << 1);
        }
    }

    public boolean is_bots_inline() {
        return (flags & (1 << 2)) != 0;
    }

    public void set_bots_inline(boolean v) {
        if (v) {
            flags |= (1 << 2);
        } else {
            flags &= ~(1 << 2);
        }
    }

    public boolean is_groups() {
        return (flags & (1 << 10)) != 0;
    }

    public void set_groups(boolean v) {
        if (v) {
            flags |= (1 << 10);
        } else {
            flags &= ~(1 << 10);
        }
    }

    public boolean is_channels() {
        return (flags & (1 << 15)) != 0;
    }

    public void set_channels(boolean v) {
        if (v) {
            flags |= (1 << 15);
        } else {
            flags &= ~(1 << 15);
        }
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        return new TopPeers(new TLVector<TLTopPeerCategoryPeers>(), new TLVector<TLChat>(), new TLVector<TLUser>());
    }
}