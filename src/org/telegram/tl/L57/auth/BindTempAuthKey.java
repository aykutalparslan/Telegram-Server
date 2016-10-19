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

public class BindTempAuthKey extends TLObject {

    public static final int ID = 0xcdd42a05;

    public long perm_auth_key_id;
    public long nonce;
    public int expires_at;
    public byte[] encrypted_message;

    public BindTempAuthKey() {
    }

    public BindTempAuthKey(long perm_auth_key_id, long nonce, int expires_at, byte[] encrypted_message) {
        this.perm_auth_key_id = perm_auth_key_id;
        this.nonce = nonce;
        this.expires_at = expires_at;
        this.encrypted_message = encrypted_message;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        perm_auth_key_id = buffer.readLong();
        nonce = buffer.readLong();
        expires_at = buffer.readInt();
        encrypted_message = buffer.readBytes();
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
        buff.writeLong(perm_auth_key_id);
        buff.writeLong(nonce);
        buff.writeInt(expires_at);
        buff.writeBytes(encrypted_message);
    }


    public int getConstructor() {
        return ID;
    }
}