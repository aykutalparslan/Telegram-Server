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

package org.telegram.tl.account;

import org.telegram.core.TLContext;
import org.telegram.core.TLMethod;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class RegisterDevice extends TLObject implements TLMethod {

    public static final int ID = 1147957548;

    public int token_type;
    public String token;
    public String device_model;
    public String system_version;
    public String app_version;
    public boolean app_sandbox;
    public String lang_code;

    public RegisterDevice() {
    }

    public RegisterDevice(int token_type, String token, String device_model, String system_version, String app_version, boolean app_sandbox, String lang_code){
        this.token_type = token_type;
        this.token = token;
        this.device_model = device_model;
        this.system_version = system_version;
        this.app_version = app_version;
        this.app_sandbox = app_sandbox;
        this.lang_code = lang_code;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        token_type = buffer.readInt();
        token = buffer.readString();
        device_model = buffer.readString();
        system_version = buffer.readString();
        app_version = buffer.readString();
        app_sandbox = buffer.readBool();
        lang_code = buffer.readString();
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
        buff.writeInt(token_type);
        buff.writeString(token);
        buff.writeString(device_model);
        buff.writeString(system_version);
        buff.writeString(app_version);
        buff.writeBool(app_sandbox);
        buff.writeString(lang_code);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        return new BoolTrue();
    }
}