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

public class PasswordInputSettings extends org.telegram.tl.account.TLPasswordInputSettings {

    public static final int ID = 0x86916deb;

    public int flags;
    public byte[] new_salt;
    public byte[] new_password_hash;
    public String hint;
    public String email;

    public PasswordInputSettings() {
    }

    public PasswordInputSettings(int flags, byte[] new_salt, byte[] new_password_hash, String hint, String email) {
        this.flags = flags;
        this.new_salt = new_salt;
        this.new_password_hash = new_password_hash;
        this.hint = hint;
        this.email = email;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        if ((flags & (1 << 0)) != 0) {
            new_salt = buffer.readBytes();
        }
        if ((flags & (1 << 0)) != 0) {
            new_password_hash = buffer.readBytes();
        }
        if ((flags & (1 << 0)) != 0) {
            hint = buffer.readString();
        }
        if ((flags & (1 << 1)) != 0) {
            email = buffer.readString();
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(40);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (new_salt.length != 0) {
            flags |= (1 << 0);
        }
        if (new_password_hash.length != 0) {
            flags |= (1 << 0);
        }
        if (hint != null && !hint.isEmpty()) {
            flags |= (1 << 0);
        }
        if (email != null && !email.isEmpty()) {
            flags |= (1 << 1);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        if ((flags & (1 << 0)) != 0) {
            buff.writeBytes(new_salt);
        }
        if ((flags & (1 << 0)) != 0) {
            buff.writeBytes(new_password_hash);
        }
        if ((flags & (1 << 0)) != 0) {
            buff.writeString(hint);
        }
        if ((flags & (1 << 1)) != 0) {
            buff.writeString(email);
        }
    }


    public int getConstructor() {
        return ID;
    }
}