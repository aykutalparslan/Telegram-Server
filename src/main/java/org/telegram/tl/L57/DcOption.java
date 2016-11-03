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

public class DcOption extends org.telegram.tl.TLDcOption {

    public static final int ID = 0x5d8c6cc;

    public int flags;
    public int id;
    public String ip_address;
    public int port;

    public DcOption() {
    }

    public DcOption(int flags, int id, String ip_address, int port) {
        this.flags = flags;
        this.id = id;
        this.ip_address = ip_address;
        this.port = port;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = buffer.readInt();
        ip_address = buffer.readString();
        port = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(48);
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
        buff.writeInt(id);
        buff.writeString(ip_address);
        buff.writeInt(port);
    }

    public boolean is_ipv6() {
        return (flags & (1 << 0)) != 0;
    }

    public void set_ipv6(boolean v) {
        if (v) {
            flags |= (1 << 0);
        } else {
            flags &= ~(1 << 0);
        }
    }

    public boolean is_media_only() {
        return (flags & (1 << 1)) != 0;
    }

    public void set_media_only(boolean v) {
        if (v) {
            flags |= (1 << 1);
        } else {
            flags &= ~(1 << 1);
        }
    }

    public boolean is_tcpo_only() {
        return (flags & (1 << 2)) != 0;
    }

    public void set_tcpo_only(boolean v) {
        if (v) {
            flags |= (1 << 2);
        } else {
            flags &= ~(1 << 2);
        }
    }

    public int getConstructor() {
        return ID;
    }
}