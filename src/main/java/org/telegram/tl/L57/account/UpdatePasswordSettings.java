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
import org.telegram.tl.account.*;

public class UpdatePasswordSettings extends TLObject {

    public static final int ID = 0xfa7c4b86;

    public byte[] current_password_hash;
    public TLPasswordInputSettings new_settings;

    public UpdatePasswordSettings() {
    }

    public UpdatePasswordSettings(byte[] current_password_hash, TLPasswordInputSettings new_settings) {
        this.current_password_hash = current_password_hash;
        this.new_settings = new_settings;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        current_password_hash = buffer.readBytes();
        new_settings = (TLPasswordInputSettings) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(36);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeBytes(current_password_hash);
        buff.writeTLObject(new_settings);
    }


    public int getConstructor() {
        return ID;
    }
}