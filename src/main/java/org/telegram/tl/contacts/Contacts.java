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

package org.telegram.tl.contacts;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class Contacts extends TLContacts {

    public static final int ID = 1871416498;

    public TLVector<TLContact> contacts;
    public TLVector<TLUser> users;

    public Contacts() {
        this.contacts = new TLVector<>();
        this.users = new TLVector<>();
    }

    public Contacts(TLVector<TLContact> contacts, TLVector<TLUser> users){
        this.contacts = contacts;
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        contacts = (TLVector<TLContact>) buffer.readTLObject(APIContext.getInstance());
        users = (TLVector<TLUser>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(contacts);
        buff.writeTLObject(users);
    }

    public int getConstructor() {
        return ID;
    }
}