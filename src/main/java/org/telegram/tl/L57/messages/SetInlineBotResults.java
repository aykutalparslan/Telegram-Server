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

public class SetInlineBotResults extends TLObject {

    public static final int ID = 0xeb5ea206;

    public int flags;
    public long query_id;
    public TLVector<org.telegram.tl.TLInputBotInlineResult> results;
    public int cache_time;
    public String next_offset;
    public org.telegram.tl.TLInlineBotSwitchPM switch_pm;

    public SetInlineBotResults() {
        this.results = new TLVector<>();
    }

    public SetInlineBotResults(int flags, long query_id, TLVector<org.telegram.tl.TLInputBotInlineResult> results, int cache_time, String next_offset, org.telegram.tl.TLInlineBotSwitchPM switch_pm) {
        this.flags = flags;
        this.query_id = query_id;
        this.results = results;
        this.cache_time = cache_time;
        this.next_offset = next_offset;
        this.switch_pm = switch_pm;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        query_id = buffer.readLong();
        results = (TLVector<org.telegram.tl.TLInputBotInlineResult>) buffer.readTLObject(APIContext.getInstance());
        cache_time = buffer.readInt();
        if ((flags & (1 << 2)) != 0) {
            next_offset = buffer.readString();
        }
        if ((flags & (1 << 3)) != 0) {
            switch_pm = (org.telegram.tl.TLInlineBotSwitchPM) buffer.readTLObject(APIContext.getInstance());
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(60);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (next_offset != null && !next_offset.isEmpty()) {
            flags |= (1 << 2);
        }
        if (switch_pm != null) {
            flags |= (1 << 3);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeLong(query_id);
        buff.writeTLObject(results);
        buff.writeInt(cache_time);
        if ((flags & (1 << 2)) != 0) {
            buff.writeString(next_offset);
        }
        if ((flags & (1 << 3)) != 0) {
            buff.writeTLObject(switch_pm);
        }
    }

    public boolean is_gallery() {
        return (flags & (1 << 0)) != 0;
    }

    public void set_gallery(boolean v) {
        if (v) {
            flags |= (1 << 0);
        } else {
            flags &= ~(1 << 0);
        }
    }

    public boolean is_private() {
        return (flags & (1 << 1)) != 0;
    }

    public void set_private(boolean v) {
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