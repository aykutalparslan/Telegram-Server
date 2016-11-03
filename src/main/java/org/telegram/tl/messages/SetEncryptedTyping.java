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
import org.telegram.data.DatabaseConnection;
import org.telegram.data.SecretChatModel;
import org.telegram.data.UserModel;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class SetEncryptedTyping extends TLObject implements TLMethod {

    public static final int ID = 2031374829;

    public TLInputEncryptedChat peer;
    public boolean typing;

    public SetEncryptedTyping() {
    }

    public SetEncryptedTyping(TLInputEncryptedChat peer, boolean typing){
        this.peer = peer;
        this.typing = typing;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLInputEncryptedChat) buffer.readTLObject(APIContext.getInstance());
        typing = buffer.readBool();
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
        buff.writeBool(typing);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        int date = (int) (System.currentTimeMillis() / 1000L);

        int chat_id = ((InputEncryptedChat) peer).chat_id;
        SecretChatModel sc = DatabaseConnection.getInstance().getSecretChat(chat_id);
        UpdateEncryptedChatTyping updateEncryptedChatTyping = new UpdateEncryptedChatTyping(chat_id);
        int user_id = 0;
        if (context.getUserId() == sc.admin_id) {
            user_id = sc.participant_id;
        } else if (context.getUserId() == sc.participant_id) {
            user_id = sc.admin_id;
        }

        TLVector<TLUser> userTLVector = new TLVector<>();
        UserModel um = UserStore.getInstance().getUser(context.getUserId());
        userTLVector.add(um.toUser(context.getApiLayer()));
        UserModel umc = UserStore.getInstance().getUser(user_id);
        userTLVector.add(umc.toUser(context.getApiLayer()));
        TLVector<TLUpdate> updateTLVector = new TLVector<>();
        updateTLVector.add(updateEncryptedChatTyping);
        TLVector<TLChat> chatTLVector = new TLVector<>();

        Updates updates = new Updates(updateTLVector, userTLVector, chatTLVector, date, 0);
        Router.getInstance().Route(user_id, updates, false);

        return new BoolTrue();
    }
}