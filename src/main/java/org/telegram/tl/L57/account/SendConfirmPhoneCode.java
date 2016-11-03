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

package org.telegram.tl.L57.account;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SendConfirmPhoneCode extends TLObject {

    public static final int ID = 0x1516d7bd;

    public int flags;
    public String hash;
    public boolean current_number;

    public SendConfirmPhoneCode() {
    }

    public SendConfirmPhoneCode(int flags, String hash, boolean current_number) {
        this.flags = flags;
        this.hash = hash;
        this.current_number = current_number;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        hash = buffer.readString();
        if ((flags & (1 << 0)) != 0) {
            current_number = buffer.readBool();
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
        if (current_number) {
            flags |= (1 << 0);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeString(hash);
        if ((flags & (1 << 0)) != 0) {
            buff.writeBool(current_number);
        }
    }

    public boolean is_allow_flashcall() {
        return (flags & (1 << 0)) != 0;
    }

    public void set_allow_flashcall(boolean v) {
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