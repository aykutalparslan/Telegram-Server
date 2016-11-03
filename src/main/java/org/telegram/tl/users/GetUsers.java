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

package org.telegram.tl.users;

import org.telegram.core.TLContext;
import org.telegram.core.TLMethod;
import org.telegram.core.UserStore;
import org.telegram.data.UserModel;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class GetUsers extends TLObject implements TLMethod {

    public static final int ID = 227648840;

    public TLVector<TLInputUser> id;

    public GetUsers() {
        this.id = new TLVector<>();
    }

    public GetUsers(TLVector<TLInputUser> id){
        this.id = id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = (TLVector<TLInputUser>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(id);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        TLVector<TLUser> users = new TLVector<>();
        for (TLInputUser u : this.id) {
            if (u instanceof InputUserContact) {
                UserModel um = UserStore.getInstance().getUser(((InputUserContact) u).user_id);
                users.add(um.toUser(context.getApiLayer()));
            } else if (u instanceof InputUserForeign) {
                UserModel um = UserStore.getInstance().getUser(((InputUserForeign) u).user_id);
                users.add(um.toUser(context.getApiLayer()));
            } else if (u instanceof InputUser) {
                UserModel um = UserStore.getInstance().getUser(((InputUser) u).user_id);
                users.add(um.toUser(context.getApiLayer()));
            }
        }

        return users;
    }
}