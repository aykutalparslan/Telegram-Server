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

import org.telegram.core.TLContext;
import org.telegram.core.TLMethod;
import org.telegram.core.UserStore;
import org.telegram.data.ContactModel;
import org.telegram.data.DatabaseConnection;
import org.telegram.data.UserModel;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;
import org.telegram.tl.service.rpc_error;

public class GetBlocked extends TLObject implements TLMethod {

    public static final int ID = -176409329;

    public int offset;
    public int limit;

    public GetBlocked() {
    }

    public GetBlocked(int offset, int limit){
        this.offset = offset;
        this.limit = limit;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        offset = buffer.readInt();
        limit = buffer.readInt();
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
        buff.writeInt(offset);
        buff.writeInt(limit);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        if (!context.isAuthorized()) {
            return new rpc_error(401, "UNAUTHORIZED");
        }
        ContactModel[] contactsAll = DatabaseConnection.getInstance().getBlockedContacts(context.getUserId(), offset, limit);
        TLVector<TLContactBlocked> contacts = new TLVector<>();
        TLVector<TLUser> users = new TLVector<>();

        for (ContactModel u : contactsAll) {
            UserModel umc = UserStore.getInstance().getUser(u.phone);
            if (umc != null) {
                contacts.add(new ContactBlocked(umc.user_id, (int) (System.currentTimeMillis() / 1000L) - 86400));
                users.add(new UserContact(umc.user_id, umc.first_name, umc.last_name,
                        umc.username, umc.access_hash, umc.phone, new UserProfilePhotoEmpty(), umc.status));
            }

        }
        return new Blocked(contacts, users);
    }
}