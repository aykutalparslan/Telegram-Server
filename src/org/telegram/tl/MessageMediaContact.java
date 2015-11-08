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

package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class MessageMediaContact extends TLMessageMedia {

    public static final int ID = 1585262393;

    public String phone_number;
    public String first_name;
    public String last_name;
    public int user_id;

    public MessageMediaContact() {
    }

    public MessageMediaContact(String phone_number, String first_name, String last_name, int user_id){
        this.phone_number = phone_number;
        this.first_name = first_name;
        this.last_name = last_name;
        this.user_id = user_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        phone_number = buffer.readString();
        first_name = buffer.readString();
        last_name = buffer.readString();
        user_id = buffer.readInt();
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
        buff.writeString(phone_number);
        buff.writeString(first_name);
        buff.writeString(last_name);
        buff.writeInt(user_id);
    }

    public int getConstructor() {
        return ID;
    }
}