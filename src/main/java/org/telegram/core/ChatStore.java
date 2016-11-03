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

package org.telegram.core;

import com.hazelcast.core.IAtomicLong;
import com.hazelcast.core.IMap;
import org.telegram.data.DatabaseConnection;
import org.telegram.data.HazelcastConnection;
import org.telegram.data.UserModel;
import org.telegram.tl.*;

/**
 * Created by aykut on 09/11/15.
 */
public class ChatStore {
    private IMap<Integer, TLChat> chatsShared;
    private IMap<Integer, int[]> chatParticipantsShared;
    private DatabaseConnection db;
    IAtomicLong chatId;

    private static ChatStore _instance;

    private ChatStore() {
        db = DatabaseConnection.getInstance();
        chatsShared = HazelcastConnection.getInstance().getMap("telegram_chats");
        chatParticipantsShared = HazelcastConnection.getInstance().getMap("telegram_chat_participants");
        chatId = HazelcastConnection.getInstance().getAtomicLong("chat_id");
        chatId.compareAndSet(0, db.getLastChatId());
    }

    public static ChatStore getInstance() {
        if (_instance == null) {
            _instance = new ChatStore();
        }
        return _instance;
    }

    public TLChat getChat(int chat_id) {
        TLChat chat = chatsShared.get(chat_id);
        if (chat == null) {
            chat = getChatFromDb(chat_id);
        }
        return chat;
    }

    public TLChat getChatFromDb(int chat_id) {
        TLChat chat = db.getChat(chat_id);
        if (chat != null) {
            chatsShared.set(chat_id, chat);
        }

        return chat;
    }

    public int[] getChatParticipants(int chat_id) {
        int[] participants = chatParticipantsShared.get(chat_id);
        if (participants == null) {
            participants = db.getChatParticipants(chat_id);
            if (participants != null) {
                chatParticipantsShared.set(chat_id, participants);
            }
        }
        return participants;
    }

    public TLChat createChat(TLChat chat, int[] users, int admin_id) {
        if (chat instanceof Chat) {
            ((Chat) chat).id = (int) chatId.incrementAndGet();
            db.createChat(((Chat) chat).id, ((Chat) chat).title, 0,
                    ((Chat) chat).date, ((Chat) chat).version, users, admin_id);

            return getChat(((Chat) chat).id);
        }

        return new ChatEmpty();
    }

    public TLChat addChatUser(int chat_id, int user_id) {
        TLChat chat = getChat(chat_id);
        if (chat != null) {
            db.addChatUser(chat_id, user_id);
        }
        return chat;
    }

    public TLChat deleteChatUser(int chat_id, int user_id) {
        TLChat chat = getChat(chat_id);
        if (chat != null) {
            db.deleteChatUser(chat_id, user_id);
        }
        return chat;
    }

    public TLChat editChatTitle(int chat_id, String chat_title) {
        TLChat chat = getChat(chat_id);
        if (chat != null) {
            db.editChatTitle(chat_id, chat_title);
        }
        chat = getChatFromDb(chat_id);
        return chat;
    }

    public TLChat editChatPhoto(int chat_id, long photo) {
        TLChat chat = getChat(chat_id);
        if (chat != null) {
            db.editChatPhoto(chat_id, photo);
        }
        chat = getChatFromDb(chat_id);
        return chat;
    }
}
