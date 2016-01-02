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

import org.telegram.core.TLContext;
import org.telegram.core.TLMethod;
import org.telegram.core.UserStore;
import org.telegram.data.DatabaseConnection;
import org.telegram.data.UserModel;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;
import org.telegram.tl.service.rpc_error;

import java.util.Random;

public class CreateChat extends TLObject implements TLMethod {

    public static final int ID = 0x9cb126e;

    public TLVector<TLInputUser> users;
    public String title;

    public CreateChat() {
        this.users = new TLVector<>();
    }

    public CreateChat(TLVector<TLInputUser> users, String title){
        this.users = users;
        this.title = title;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        users = (TLVector<TLInputUser>) buffer.readTLObject(APIContext.getInstance());
        title = buffer.readString();
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
        buff.writeTLObject(users);
        buff.writeString(title);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        if (context.isAuthorized()) {
            int date = (int) (System.currentTimeMillis() / 1000L);
            int[] users_ids = new int[users.size() + 1];
            users_ids[0] = context.getUserId();
            for (int i = 0; i < users.size(); i++) {
                if (users.get(i) instanceof InputUser) {
                    users_ids[i + 1] = ((InputUser) users.get(i)).user_id;
                } else if (users.get(i) instanceof InputUserContact) {
                    users_ids[i + 1] = ((InputUserContact) users.get(i)).user_id;
                } else if (users.get(i) instanceof InputUserForeign) {
                    users_ids[i + 1] = ((InputUserForeign) users.get(i)).user_id;
                }
            }
            Random rnd = new Random();//TODO: add a chat id counter
            int chat_id = rnd.nextInt();
            DatabaseConnection.getInstance().createChat(chat_id, title, 0, date, 0, users_ids);

            TLVector<TLChat> chatTLVector = new TLVector<>();
            chatTLVector.add(new Chat(chat_id, title, new ChatPhotoEmpty(), users_ids.length, date, false, 1));
            TLVector<TLUser> userTLVector = new TLVector<>();
            TLVector<TLUpdate> updateTLVector = new TLVector<>();
            TLVector<TLChatParticipant> participants = new TLVector<>();
            TLVector<Integer> usersVectorInteger = new TLVector<>();
            for (int user_id : users_ids) {
                usersVectorInteger.add(user_id);
                UserModel umc = UserStore.getInstance().getUser(user_id);
                if (umc != null) {
                    userTLVector.add(umc.toUser());
                }
                participants.add(new ChatParticipant(user_id, context.getUserId(), date));
            }
            UpdateChatParticipants ucp = new UpdateChatParticipants(new ChatParticipants(chat_id, context.getUserId(), participants, 1));
            updateTLVector.add(ucp);
            UserModel um = UserStore.getInstance().increment_pts_getUser(context.getUserId(), 1, 1, 0);
            int message_id = um.sent_messages + um.received_messages + 1;
            int flags = 0;
            UpdateNewMessage chat_created = new UpdateNewMessage(new MessageService(flags, message_id, context.getUserId(),
                    new PeerChat(chat_id), date, new MessageActionChatCreate(title, usersVectorInteger)), um.pts, 1);

            updateTLVector.add(chat_created);
            Updates updates = new Updates(updateTLVector, userTLVector, chatTLVector, date, um.pts);

            return updates;
        }
        return rpc_error.UNAUTHORIZED();
    }
}