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

public class EditChatTitle extends TLObject implements TLMethod {

    public static final int ID = 0xdc452855;

    public int chat_id;
    public String title;

    public EditChatTitle() {
    }

    public EditChatTitle(int chat_id, String title){
        this.chat_id = chat_id;
        this.title = title;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        chat_id = buffer.readInt();
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
        buff.writeInt(chat_id);
        buff.writeString(title);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        if (context.isAuthorized()) {
            int date = (int) (System.currentTimeMillis() / 1000L);

            TLChat chat = ChatStore.getInstance().editChatTitle(chat_id, title);

            UserModel um = UserStore.getInstance().increment_pts_getUser(context.getUserId(), 1, 1, 0);
            int message_id = um.sent_messages + um.received_messages + 1;
            int flags = 259;

            TLVector<TLChat> chatTLVector = new TLVector<>();
            if (chat != null) {
                chatTLVector.add(chat);
            }
            TLVector<TLUser> userTLVector = new TLVector<>();

            userTLVector.add(um.toUser(context.getApiLayer()));
            TLVector<TLUpdate> updateTLVector = new TLVector<>();

            UpdateNewMessage title_changed = new UpdateNewMessage(new MessageService(flags, message_id, context.getUserId(),
                    new PeerChat(chat_id), date, new MessageActionChatEditTitle(title)), um.pts, 1);

            updateTLVector.add(title_changed);

            Updates updates = new Updates(updateTLVector, userTLVector, chatTLVector, date, 0);
            int[] users_ids = ChatStore.getInstance().getChatParticipants(chat_id);

            for (int user_id : users_ids) {
                if (user_id != context.getUserId() && user_id != 0) {
                    UserModel umc2 = UserStore.getInstance().increment_pts_getUser(user_id, 1, 1, 0);
                    TLVector<TLUpdate> updateTLVector2 = new TLVector<>();
                    int message_id2 = umc2.sent_messages + umc2.received_messages + 1;
                    int flags2 = 0;

                    UpdateNewMessage title_changed2 = new UpdateNewMessage(new MessageService(flags2, message_id, context.getUserId(),
                            new PeerChat(chat_id), date, new MessageActionChatEditTitle(title)), umc2.pts, 1);

                    updateTLVector2.add(title_changed2);

                    Updates updates2 = new Updates(updateTLVector2, userTLVector, chatTLVector, date, 0);
                    Router.getInstance().Route(user_id, updates2, false);
                }
            }

            return updates;
        }
        return rpc_error.UNAUTHORIZED();
    }
}