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

package org.telegram.tl.L57.auth;

import org.telegram.core.TLContext;
import org.telegram.core.TLMethod;
import org.telegram.core.UserStore;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SendCode extends TLObject implements TLMethod {

    public static final int ID = 0x86aef0ec;

    public int flags;
    public String phone_number;
    public boolean current_number;
    public int api_id;
    public String api_hash;

    public SendCode() {
    }

    public SendCode(int flags, String phone_number, boolean current_number, int api_id, String api_hash) {
        this.flags = flags;
        this.phone_number = phone_number;
        this.current_number = current_number;
        this.api_id = api_id;
        this.api_hash = api_hash;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        phone_number = buffer.readString();
        if ((flags & (1 << 0)) != 0) {
            current_number = buffer.readBool();
        }
        api_id = buffer.readInt();
        api_hash = buffer.readString();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(44);
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
        buff.writeString(phone_number);
        if ((flags & (1 << 0)) != 0) {
            buff.writeBool(current_number);
        }
        buff.writeInt(api_id);
        buff.writeString(api_hash);
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

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {

        if (UserStore.getInstance().getUser(clearPhone(phone_number)) == null) {
            SentCode sentCode = new SentCode(0, new SentCodeTypeSms(), "EFEFEFEFEFEFEFEFEF", new CodeTypeCall(), 0);
            sentCode.set_phone_registered(true);
            return sentCode;
        } else {
            SentCode sentCode = new SentCode(0, new SentCodeTypeSms(), "EFEFEFEFEFEFEFEFEF", new CodeTypeCall(), 0);
            return sentCode;
        }


    }

    public String clearPhone(String phone) {
        return phone.replace("(", "").replace(")", "").replace(" ", "").replace("-", "").replace("+", "");
    }
}