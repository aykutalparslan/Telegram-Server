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
import org.telegram.tl.contacts.*;
import org.telegram.tl.service.rpc_error;

public class GetFullUser extends TLObject implements TLMethod {

    public static final int ID = -902781519;

    public TLInputUser id;

    public GetFullUser() {
    }

    public GetFullUser(TLInputUser id){
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
        int user_id = 0;
        if (this.id instanceof InputUserContact) {
            user_id = ((InputUserContact) this.id).user_id;
        } else if (this.id instanceof InputUserForeign) {
            user_id = ((InputUserForeign) this.id).user_id;
        } else if (this.id instanceof InputUserSelf) {
            UserModel um = UserStore.getInstance().getUser(context.getUserId());
            return new UserFull(um.toUser(context.getApiLayer()), new Link(new MyLinkContact(), new ForeignLinkMutual(), um.toUser(context.getApiLayer())),
                    new PhotoEmpty(), new PeerNotifySettingsEmpty(), false, new BotInfoEmpty());
        } else if (this.id instanceof InputUser) {
            user_id = ((InputUser) this.id).user_id;
        }
        UserModel um = UserStore.getInstance().getUser(user_id);
        if (um != null) {
            if (context.getApiLayer() >= 57) {
                org.telegram.tl.L57.UserFull userFull = new org.telegram.tl.L57.UserFull(0, um.toUser(context.getApiLayer()), "",
                        new org.telegram.tl.L57.contacts.Link(new org.telegram.tl.L57.ContactLinkContact(), new org.telegram.tl.L57.ContactLinkContact(), um.toUser(context.getApiLayer())),
                        null, new PeerNotifySettingsEmpty(), null);
                return userFull;
            } else {
                UserFull userFull = new UserFull(um.toUser(context.getApiLayer()), new Link(new MyLinkContact(), new ForeignLinkMutual(), um.toUser(context.getApiLayer())),
                        new PhotoEmpty(), new PeerNotifySettingsEmpty(), false, new BotInfoEmpty());
                return userFull;
            }

        }
        return null;
    }
}