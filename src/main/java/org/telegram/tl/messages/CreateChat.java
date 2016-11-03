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

import org.telegram.core.*;
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

            boolean contains_self = false;
            for (int i = 0; i < users.size(); i++) {
                if (users.get(i) instanceof InputUserSelf) {
                    contains_self = true;
                }
            }
            int[] users_ids;
            if (contains_self) {
                users_ids = new int[users.size()];
                for (int i = 0; i < users.size(); i++) {
                    if (users.get(i) instanceof InputUser) {
                        users_ids[i] = ((InputUser) users.get(i)).user_id;
                    } else if (users.get(i) instanceof InputUserContact) {
                        users_ids[i] = ((InputUserContact) users.get(i)).user_id;
                    } else if (users.get(i) instanceof InputUserForeign) {
                        users_ids[i] = ((InputUserForeign) users.get(i)).user_id;
                    } else if (users.get(i) instanceof InputUserSelf) {
                        users_ids[i] = context.getUserId();
                    }
                }
            } else {
                users_ids = new int[users.size() + 1];
                for (int i = 0; i < users.size(); i++) {
                    if (users.get(i) instanceof InputUser) {
                        users_ids[i] = ((InputUser) users.get(i)).user_id;
                    } else if (users.get(i) instanceof InputUserContact) {
                        users_ids[i] = ((InputUserContact) users.get(i)).user_id;
                    } else if (users.get(i) instanceof InputUserForeign) {
                        users_ids[i] = ((InputUserForeign) users.get(i)).user_id;
                    }
                }
                users_ids[users_ids.length - 1] = context.getUserId();
            }

            Random rnd = new Random();

            TLVector<TLChat> chatTLVector = new TLVector<>();
            TLChat newChat = ChatStore.getInstance().createChat(new Chat(0, title, new ChatPhotoEmpty(),
                    users_ids.length, date, false, 1), users_ids, context.getUserId());
            int chat_id = ((Chat) newChat).id;
            if (context.getApiLayer() >= 48) {
                newChat = ((Chat) newChat).toChatL42();
            }
            chatTLVector.add(newChat);

            TLVector<TLUser> userTLVector = new TLVector<>();
            TLVector<TLUpdate> updateTLVector = new TLVector<>();
            TLVector<Integer> usersVectorInteger = new TLVector<>();
            TLVector<TLChatParticipant> participants = new TLVector<>();
            for (int user_id : users_ids) {
                usersVectorInteger.add(user_id);
                UserModel umc = UserStore.getInstance().getUser(user_id);
                if (umc != null) {
                    userTLVector.add(umc.toUser(context.getApiLayer()));
                    participants.add(new ChatParticipant(user_id, context.getUserId(), date));
                }
            }
            UserModel um = UserStore.getInstance().increment_pts_getUser(context.getUserId(), 1, 1, 0);
            int message_id = um.sent_messages + um.received_messages + 1;
            int flags = 259;

            if (context.getApiLayer() >= 48) {
                UpdateNewMessage chat_created = new UpdateNewMessage(new MessageServiceL45(flags, message_id, context.getUserId(),
                        new PeerChat(chat_id), date, new MessageActionChatCreate(title, usersVectorInteger)), um.pts, 1);

                updateTLVector.add(chat_created);

                UpdateChatParticipants ucp = new UpdateChatParticipants(new org.telegram.tl.L57.ChatParticipants(chat_id, participants, 1));
                updateTLVector.add(ucp);
            } else {
                UpdateNewMessage chat_created = new UpdateNewMessage(new MessageService(flags, message_id, context.getUserId(),
                        new PeerChat(chat_id), date, new MessageActionChatCreate(title, usersVectorInteger)), um.pts, 1);

                updateTLVector.add(chat_created);

                UpdateChatParticipants ucp = new UpdateChatParticipants(new ChatParticipants(chat_id, context.getUserId(), participants, 1));
                updateTLVector.add(ucp);
            }


            Updates updates = new Updates(updateTLVector, userTLVector, chatTLVector, date, 0);

            for (int user_id : users_ids) {
                if (user_id != context.getUserId() && user_id != 0) {
                    UserModel umc = UserStore.getInstance().increment_pts_getUser(user_id, 1, 1, 0);
                    TLVector<TLUpdate> updateTLVector2 = new TLVector<>();
                    int message_id2 = umc.sent_messages + umc.received_messages + 1;
                    int flags2 = 0;
                    for (Object sess : Router.getInstance().getActiveSessions(user_id)) {
                        if (((ActiveSession) sess).layer >= 48) {
                            MessageServiceL45 msg_service = new MessageServiceL45(flags2, message_id2, context.getUserId(),
                                    new PeerChat(chat_id), date, new MessageActionChatCreate(title, usersVectorInteger));

                            UpdateNewMessage chat_created2 = new UpdateNewMessage(msg_service, umc.pts, 1);

                            UpdateChatParticipants ucp = new UpdateChatParticipants(new org.telegram.tl.L57.ChatParticipants(chat_id, participants, 1));
                            updateTLVector.add(ucp);

                            updateTLVector2.add(chat_created2);
                            updateTLVector2.add(ucp);

                            Updates updates2 = new Updates(updateTLVector2, userTLVector, chatTLVector, date, 0);
                            Router.getInstance().Route(((ActiveSession) sess).session_id, ((ActiveSession) sess).auth_key_id, updates2, false);
                        } else {
                            MessageService msg_service = new MessageService(flags2, message_id2, context.getUserId(),
                                    new PeerChat(chat_id), date, new MessageActionChatCreate(title, usersVectorInteger));

                            UpdateNewMessage chat_created2 = new UpdateNewMessage(msg_service, umc.pts, 1);

                            UpdateChatParticipants ucp = new UpdateChatParticipants(new ChatParticipants(chat_id, context.getUserId(), participants, 1));
                            updateTLVector.add(ucp);

                            updateTLVector2.add(chat_created2);
                            updateTLVector2.add(ucp);

                            Updates updates2 = new Updates(updateTLVector2, userTLVector, chatTLVector, date, 0);
                            Router.getInstance().Route(((ActiveSession) sess).session_id, ((ActiveSession) sess).auth_key_id, updates2, false);
                        }
                    }

                }
            }

            return updates;
        }
        return rpc_error.UNAUTHORIZED();
    }
}