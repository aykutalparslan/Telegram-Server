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
import org.telegram.data.UserModel;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;
import org.telegram.tl.service.rpc_error;

import java.util.Random;

public class AddChatUser extends TLObject implements TLMethod {

    public static final int ID = 0xf9a0aa09;

    public int chat_id;
    public TLInputUser user_id;
    public int fwd_limit;

    public AddChatUser() {
    }

    public AddChatUser(int chat_id, TLInputUser user_id, int fwd_limit){
        this.chat_id = chat_id;
        this.user_id = user_id;
        this.fwd_limit = fwd_limit;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        chat_id = buffer.readInt();
        user_id = (TLInputUser) buffer.readTLObject(APIContext.getInstance());
        fwd_limit = buffer.readInt();
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
        buff.writeInt(chat_id);
        buff.writeTLObject(user_id);
        buff.writeInt(fwd_limit);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        if (context.isAuthorized()) {
            int date = (int) (System.currentTimeMillis() / 1000L);

            UserModel umc = UserStore.getInstance().getUser(((InputUser) user_id).user_id);
            TLChat chat = null;
            if (umc != null) {
                chat = ChatStore.getInstance().addChatUser(chat_id, umc.user_id);
            }

            UserModel um = UserStore.getInstance().increment_pts_getUser(context.getUserId(), 1, 1, 0);
            int message_id = um.sent_messages + um.received_messages + 1;
            int flags = 259;

            TLVector<TLChat> chatTLVector = new TLVector<>();
            if (chat != null) {
                if (context.getApiLayer() >= 48) {
                    chatTLVector.add(((Chat) chat).toChatL42());
                } else {
                    chatTLVector.add(chat);
                }
            }
            TLVector<TLUser> userTLVector = new TLVector<>();
            userTLVector.add(umc.toUser(context.getApiLayer()));
            userTLVector.add(um.toUser(context.getApiLayer()));
            TLVector<TLUpdate> updateTLVector = new TLVector<>();
            if (context.getApiLayer() >= 48) {
                UpdateNewMessage user_added = new UpdateNewMessage(new MessageServiceL45(flags, message_id, context.getUserId(),
                        new PeerChat(chat_id), date, new MessageActionChatAddUser(umc.user_id)), um.pts, 1);
                updateTLVector.add(user_added);

                org.telegram.tl.L57.UpdateChatParticipantAdd ucpa = new org.telegram.tl.L57.UpdateChatParticipantAdd(chat_id, umc.user_id, um.user_id, date, 1);
                updateTLVector.add(ucpa);
            } else {
                UpdateNewMessage user_added = new UpdateNewMessage(new MessageService(flags, message_id, context.getUserId(),
                        new PeerChat(chat_id), date, new MessageActionChatAddUser(umc.user_id)), um.pts, 1);
                updateTLVector.add(user_added);

                UpdateChatParticipantAdd ucpa = new UpdateChatParticipantAdd(chat_id, umc.user_id, um.user_id, 1);
                updateTLVector.add(ucpa);
            }


            Updates updates = new Updates(updateTLVector, userTLVector, chatTLVector, date, 0);
            int[] users_ids = ChatStore.getInstance().getChatParticipants(chat_id);

            for (int user_id : users_ids) {
                if (user_id != context.getUserId() && user_id != 0) {
                    UserModel umc2 = UserStore.getInstance().increment_pts_getUser(user_id, 1, 1, 0);
                    TLVector<TLUpdate> updateTLVector2 = new TLVector<>();
                    int message_id2 = umc2.sent_messages + umc2.received_messages + 1;
                    int flags2 = 0;

                    for (Object sess : Router.getInstance().getActiveSessions(user_id)) {
                        if (((ActiveSession) sess).layer >= 48) {
                            UpdateNewMessage user_added2 = new UpdateNewMessage(new MessageServiceL45(flags2, message_id2, context.getUserId(),
                                    new PeerChat(chat_id), date, new MessageActionChatAddUser(umc.user_id)), umc2.pts, 1);

                            updateTLVector2.add(user_added2);

                            org.telegram.tl.L57.UpdateChatParticipantAdd ucpa = new org.telegram.tl.L57.UpdateChatParticipantAdd(chat_id, umc.user_id, um.user_id, date, 1);
                            updateTLVector2.add(ucpa);

                            Updates updates2 = new Updates(updateTLVector2, userTLVector, chatTLVector, date, 0);
                            Router.getInstance().Route(((ActiveSession) sess).session_id, ((ActiveSession) sess).auth_key_id, updates2, false);
                        } else {
                            UpdateNewMessage user_added2 = new UpdateNewMessage(new MessageService(flags2, message_id2, context.getUserId(),
                                    new PeerChat(chat_id), date, new MessageActionChatAddUser(umc.user_id)), umc2.pts, 1);

                            updateTLVector2.add(user_added2);

                            UpdateChatParticipantAdd ucpa = new UpdateChatParticipantAdd(chat_id, umc.user_id, um.user_id, 1);
                            updateTLVector2.add(ucpa);

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