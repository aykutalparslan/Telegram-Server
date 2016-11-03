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

public class ImportedContacts extends TLImportedContacts {

    public static final int ID = -1387117803;

    public TLVector<TLImportedContact> imported;
    public TLVector<Long> retry_contacts;
    public TLVector<TLUser> users;

    public ImportedContacts() {
        this.imported = new TLVector<>();
        this.retry_contacts = new TLVector<>();
        this.users = new TLVector<>();
    }

    public ImportedContacts(TLVector<TLImportedContact> imported, TLVector<Long> retry_contacts, TLVector<TLUser> users){
        this.imported = imported;
        this.retry_contacts = retry_contacts;
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        imported = (TLVector<TLImportedContact>) buffer.readTLObject(APIContext.getInstance());
        retry_contacts = (TLVector<Long>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(imported);
        buff.writeTLObject(retry_contacts);
        buff.writeTLObject(users);
    }

    public int getConstructor() {
        return ID;
    }
}