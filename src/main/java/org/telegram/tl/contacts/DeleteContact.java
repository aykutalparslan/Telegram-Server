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

import org.telegram.core.SessionStore;
import org.telegram.core.TLContext;
import org.telegram.core.TLMethod;
import org.telegram.core.UserStore;
import org.telegram.data.DatabaseConnection;
import org.telegram.data.SessionModel;
import org.telegram.data.UserModel;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;
import org.telegram.tl.service.rpc_error;

public class DeleteContact extends TLObject implements TLMethod {

    public static final int ID = -1902823612;

    public TLInputUser id;

    public DeleteContact() {
    }

    public DeleteContact(TLInputUser id){
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
        if (id instanceof InputUserContact) {
            SessionModel sm = SessionStore.getInstance().getSession(context.getSessionId());
            if (sm != null) {
                UserModel um = UserStore.getInstance().getUser(sm.phone);
                if (um != null) {
                    UserModel umc = UserStore.getInstance().getUser(((InputUserContact) id).user_id);
                    DatabaseConnection.getInstance().deleteContact(um.user_id, umc.phone);
                    return new Link(new MyLinkContact(), new ForeignLinkUnknown(), umc.toUser(context.getApiLayer()));
                }
            }

        }
        return new rpc_error(500, "INTERNAL_SERVER_ERROR");
    }
}