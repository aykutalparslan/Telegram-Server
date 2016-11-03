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

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ImportBotAuthorization extends TLObject {

    public static final int ID = 0x67a3ff2c;

    public int flags;
    public int api_id;
    public String api_hash;
    public String bot_auth_token;

    public ImportBotAuthorization() {
    }

    public ImportBotAuthorization(int flags, int api_id, String api_hash, String bot_auth_token) {
        this.flags = flags;
        this.api_id = api_id;
        this.api_hash = api_hash;
        this.bot_auth_token = bot_auth_token;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        api_id = buffer.readInt();
        api_hash = buffer.readString();
        bot_auth_token = buffer.readString();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(28);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeInt(api_id);
        buff.writeString(api_hash);
        buff.writeString(bot_auth_token);
    }


    public int getConstructor() {
        return ID;
    }
}