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

public class ReadEncryptedHistory extends TLObject implements TLMethod {

    public static final int ID = 2135648522;

    public TLInputEncryptedChat peer;
    public int max_date;

    public ReadEncryptedHistory() {
    }

    public ReadEncryptedHistory(TLInputEncryptedChat peer, int max_date){
        this.peer = peer;
        this.max_date = max_date;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLInputEncryptedChat) buffer.readTLObject(APIContext.getInstance());
        max_date = buffer.readInt();
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
        buff.writeInt(max_date);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        int date = (int) (System.currentTimeMillis() / 1000L);

        int chat_id = ((InputEncryptedChat) peer).chat_id;
        SecretChatModel sc = DatabaseConnection.getInstance().getSecretChat(chat_id);
        UpdateEncryptedMessagesRead encryptedMessagesRead = new UpdateEncryptedMessagesRead(chat_id, max_date, date);
        int user_id = 0;
        if (context.getUserId() == sc.admin_id) {
            user_id = sc.participant_id;
        } else if (context.getUserId() == sc.participant_id) {
            user_id = sc.admin_id;
        }

        TLVector<TLUser> userTLVector = new TLVector<>();
        UserModel um = UserStore.getInstance().getUser(context.getUserId());
        userTLVector.add(um.toUser(context.getApiLayer()));
        UserModel umc = UserStore.getInstance().increment_pts_getUser(user_id, 1, 1, 0);
        userTLVector.add(umc.toUser(context.getApiLayer()));
        TLVector<TLUpdate> updateTLVector = new TLVector<>();
        updateTLVector.add(encryptedMessagesRead);
        TLVector<TLChat> chatTLVector = new TLVector<>();

        Updates updates = new Updates(updateTLVector, userTLVector, chatTLVector, date, 0);
        Router.getInstance().Route(user_id, updates, false);

        return new BoolTrue();
    }
}