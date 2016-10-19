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

public class BotResults extends org.telegram.tl.messages.TLBotResults {

    public static final int ID = 0x256709a6;

    public int flags;
    public long query_id;
    public String next_offset;
    public org.telegram.tl.TLInlineBotSwitchPM switch_pm;
    public TLVector<org.telegram.tl.TLBotInlineResult> results;

    public BotResults() {
        this.results = new TLVector<>();
    }

    public BotResults(int flags, long query_id, String next_offset, org.telegram.tl.TLInlineBotSwitchPM switch_pm, TLVector<org.telegram.tl.TLBotInlineResult> results) {
        this.flags = flags;
        this.query_id = query_id;
        this.next_offset = next_offset;
        this.switch_pm = switch_pm;
        this.results = results;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        query_id = buffer.readLong();
        if ((flags & (1 << 1)) != 0) {
            next_offset = buffer.readString();
        }
        if ((flags & (1 << 2)) != 0) {
            switch_pm = (org.telegram.tl.TLInlineBotSwitchPM) buffer.readTLObject(APIContext.getInstance());
        }
        results = (TLVector<org.telegram.tl.TLBotInlineResult>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(48);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (next_offset != null && !next_offset.isEmpty()) {
            flags |= (1 << 1);
        }
        if (switch_pm != null) {
            flags |= (1 << 2);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeLong(query_id);
        if ((flags & (1 << 1)) != 0) {
            buff.writeString(next_offset);
        }
        if ((flags & (1 << 2)) != 0) {
            buff.writeTLObject(switch_pm);
        }
        buff.writeTLObject(results);
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

    public int getConstructor() {
        return ID;
    }
}