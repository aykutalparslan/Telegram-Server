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

package org.telegram.tl.auth;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class SentAppCode extends TLSentCode {

    public static final int ID = -484053553;

    public boolean phone_registered;
    public String phone_code_hash;
    public int send_call_timeout;
    public boolean is_password;

    public SentAppCode() {
    }

    public SentAppCode(boolean phone_registered, String phone_code_hash, int send_call_timeout, boolean is_password){
        this.phone_registered = phone_registered;
        this.phone_code_hash = phone_code_hash;
        this.send_call_timeout = send_call_timeout;
        this.is_password = is_password;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        phone_registered = buffer.readBool();
        phone_code_hash = buffer.readString();
        send_call_timeout = buffer.readInt();
        is_password = buffer.readBool();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(32);
        serializeTo(buffer);
        return buffer;
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeBool(phone_registered);
        buff.writeString(phone_code_hash);
        buff.writeInt(send_call_timeout);
        buff.writeBool(is_password);
    }

    public int getConstructor() {
        return ID;
    }
}