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

import org.telegram.core.ChatStore;
import org.telegram.core.TLContext;
import org.telegram.core.TLMethod;
import org.telegram.core.UserStore;
import org.telegram.data.DatabaseConnection;
import org.telegram.data.UserModel;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.server.ServerConfig;
import org.telegram.tl.*;

import java.util.Random;

public class GetFullChat extends TLObject implements TLMethod {

    public static final int ID = 998448230;

    public int chat_id;

    public GetFullChat() {
    }

    public GetFullChat(int chat_id){
        this.chat_id = chat_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        chat_id = buffer.readInt();
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
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        int date = (int) (System.currentTimeMillis() / 1000L);
        TLChat chat = ChatStore.getInstance().getChat(chat_id);
        TLVector<TLChat> chatTLVector = new TLVector<>();

        int[] participants_arr = ChatStore.getInstance().getChatParticipants(chat_id);
        TLVector<TLUser> userTLVector = new TLVector<>();
        TLVector<Integer> usersVectorInteger = new TLVector<>();
        TLVector<TLChatParticipant> participants = new TLVector<>();
        for (int user_id : participants_arr) {
            usersVectorInteger.add(user_id);
            UserModel umc = UserStore.getInstance().getUser(user_id);
            if (umc != null) {
                userTLVector.add(umc.toUser(context.getApiLayer()));
                participants.add(new ChatParticipant(user_id, context.getUserId(), date));
            }
        }
        TLPhoto chat_photo = new PhotoEmpty();
        if (((Chat) chat).photo instanceof ChatPhoto) {
            long photo_id = ((FileLocation) ((ChatPhoto) ((Chat) chat).photo).photo_big).secret;
            if (photo_id != 0) {
                int file_size = DatabaseConnection.getInstance().getFileSize(photo_id);
                TLVector<TLPhotoSize> photoSizes = new TLVector<>();
                Random rnd = new Random();
                photoSizes.add(new PhotoSize("s", new FileLocation(ServerConfig.SERVER_ID, photo_id, rnd.nextInt(), photo_id), 96, 96, file_size));
                photoSizes.add(new PhotoSize("m", new FileLocation(ServerConfig.SERVER_ID, photo_id, rnd.nextInt(), photo_id), 256, 256, file_size));
                photoSizes.add(new PhotoSize("x", new FileLocation(ServerConfig.SERVER_ID, photo_id, rnd.nextInt(), photo_id), 512, 512, file_size));
                chat_photo = new Photo(photo_id, photo_id, date, photoSizes);
            }
        }
        if (context.getApiLayer() >= 48) {
            org.telegram.tl.ChatFull chatFull = new org.telegram.tl.ChatFull(chat_id,
                    new org.telegram.tl.L57.ChatParticipants(chat_id, participants, 1),
                    chat_photo,
                    new PeerNotifySettingsEmpty(),
                    new ChatInviteEmpty(),
                    new TLVector<TLBotInfo>());

            ChatFull fullChat = new ChatFull(chatFull, chatTLVector, userTLVector);

            chatTLVector.add(((Chat) chat).toChatL42());

            return fullChat;
        } else {
            org.telegram.tl.ChatFull chatFull = new org.telegram.tl.ChatFull(chat_id,
                    new ChatParticipants(chat_id, ((Chat) chat)._admin_id, participants, 1),
                    chat_photo,
                    new PeerNotifySettingsEmpty(),
                    new ChatInviteEmpty(),
                    new TLVector<TLBotInfo>());

            ChatFull fullChat = new ChatFull(chatFull, chatTLVector, userTLVector);

            chatTLVector.add(chat);

            return fullChat;
        }
    }
}