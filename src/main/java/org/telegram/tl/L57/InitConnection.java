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

public class InitConnection extends TLObject {

    public static final int ID = 0x69796de9;

    public int api_id;
    public String device_model;
    public String system_version;
    public String app_version;
    public String lang_code;
    public TLObject query;

    public InitConnection() {
    }

    public InitConnection(int api_id, String device_model, String system_version, String app_version, String lang_code, TLObject query) {
        this.api_id = api_id;
        this.device_model = device_model;
        this.system_version = system_version;
        this.app_version = app_version;
        this.lang_code = lang_code;
        this.query = query;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        api_id = buffer.readInt();
        device_model = buffer.readString();
        system_version = buffer.readString();
        app_version = buffer.readString();
        lang_code = buffer.readString();
        query = (TLObject) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(48);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(api_id);
        buff.writeString(device_model);
        buff.writeString(system_version);
        buff.writeString(app_version);
        buff.writeString(lang_code);
        buff.writeTLObject(query);
    }


    public int getConstructor() {
        return ID;
    }
}