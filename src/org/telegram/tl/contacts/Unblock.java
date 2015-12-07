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
import org.telegram.data.DatabaseConnection;
import org.telegram.data.UserModel;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;
import org.telegram.tl.service.rpc_error;

public class Unblock extends TLObject implements TLMethod {

    public static final int ID = -448724803;

    public TLInputUser id;

    public Unblock() {
    }

    public Unblock(TLInputUser id){
        this.id = id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = (TLInputUser) buffer.readTLObject(APIContext.getInstance());
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
        if (!context.isAuthorized()) {
            return new rpc_error(401, "UNAUTHORIZED");
        }

        if (id instanceof InputUserContact) {
            UserModel um = DatabaseConnection.getInstance().getUser(((InputUserContact) id).user_id);
            DatabaseConnection.getInstance().unblockContact(context.getUserId(), um.phone);
        } else if (id instanceof InputUserForeign) {
            UserModel um = DatabaseConnection.getInstance().getUser(((InputUserForeign) id).user_id);
            DatabaseConnection.getInstance().unblockContact(context.getUserId(), um.phone);
        } else {
            return new BoolFalse();
        }

        return new BoolTrue();
    }
}