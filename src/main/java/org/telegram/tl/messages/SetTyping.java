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

package org.telegram.tl.messages;

import org.telegram.core.Router;
import org.telegram.core.TLContext;
import org.telegram.core.TLMethod;
import org.telegram.core.UserStore;
import org.telegram.data.UserModel;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;
import org.telegram.tl.service.rpc_error;

public class SetTyping extends TLObject implements TLMethod {

    public static final int ID = 0xa3825e50;

    public TLInputPeer peer;
    public TLSendMessageAction action;

    public SetTyping() {
    }

    public SetTyping(TLInputPeer peer, TLSendMessageAction action){
        this.peer = peer;
        this.action = action;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        action = (TLSendMessageAction) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(peer);
        buff.writeTLObject(action);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        if (context.isAuthorized()) {
            if (peer instanceof InputPeerUser) {
                int date = (int) (System.currentTimeMillis() / 1000L);

                UpdateUserTyping typing = new UpdateUserTyping(context.getUserId(), new SendMessageTypingAction());
                TLVector<TLUser> userTLVector = new TLVector<>();
                UserModel um = UserStore.getInstance().getUser(context.getUserId());
                userTLVector.add(um.toUser(context.getApiLayer()));
                UserModel umc = UserStore.getInstance().getUser(((InputPeerUser) peer).user_id);
                userTLVector.add(umc.toUser(context.getApiLayer()));
                TLVector<TLUpdate> updateTLVector = new TLVector<>();
                updateTLVector.add(typing);
                TLVector<TLChat> chatTLVector = new TLVector<>();

                Updates updates = new Updates(updateTLVector, userTLVector, chatTLVector, date, 0);
                Router.getInstance().Route(((InputPeerUser) peer).user_id, updates, false);
                return new BoolTrue();
            } else if (peer instanceof InputPeerChat) {
                int date = (int) (System.currentTimeMillis() / 1000L);
                UpdateUserTyping typing = new UpdateUserTyping(context.getUserId(), new SendMessageTypingAction());
                UpdateShort updateShort = new UpdateShort(typing, date);
                /*

                 */
                return new BoolTrue();
            }
        }
        return rpc_error.UNAUTHORIZED();
    }
}