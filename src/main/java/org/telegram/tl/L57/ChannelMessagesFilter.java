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

public class ChannelMessagesFilter extends org.telegram.tl.TLChannelMessagesFilter {

    public static final int ID = 0xcd77d957;

    public int flags;
    public TLVector<org.telegram.tl.TLMessageRange> ranges;

    public ChannelMessagesFilter() {
        this.ranges = new TLVector<>();
    }

    public ChannelMessagesFilter(int flags, TLVector<org.telegram.tl.TLMessageRange> ranges) {
        this.flags = flags;
        this.ranges = ranges;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        ranges = (TLVector<org.telegram.tl.TLMessageRange>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(24);
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
        buff.writeTLObject(ranges);
    }

    public boolean is_exclude_new_messages() {
        return (flags & (1 << 1)) != 0;
    }

    public void set_exclude_new_messages(boolean v) {
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