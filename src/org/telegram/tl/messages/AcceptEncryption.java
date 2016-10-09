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

public class AcceptEncryption extends TLObject implements TLMethod {

    public static final int ID = 0x3dbc0415;

    public TLInputEncryptedChat peer;
    public byte[] g_b;
    public long key_fingerprint;

    public AcceptEncryption() {
    }

    public AcceptEncryption(TLInputEncryptedChat peer, byte[] g_b, long key_fingerprint){
        this.peer = peer;
        this.g_b = g_b;
        this.key_fingerprint = key_fingerprint;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLInputEncryptedChat) buffer.readTLObject(APIContext.getInstance());
        g_b = buffer.readBytes();
        key_fingerprint = buffer.readLong();
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
        buff.writeBytes(g_b);
        buff.writeLong(key_fingerprint);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        int date = (int) (System.currentTimeMillis() / 1000L);
        int chat_id = ((InputEncryptedChat) peer).chat_id;
        SecretChatModel sc = DatabaseConnection.getInstance().getSecretChat(chat_id);
        EncryptedChat encryptedChat = new EncryptedChat(chat_id, chat_id, date, sc.admin_id, sc.participant_id, g_b, key_fingerprint);

        UpdateEncryption updateEncryption = new UpdateEncryption(encryptedChat, date);

        TLVector<TLUser> userTLVector = new TLVector<>();
        UserModel um = UserStore.getInstance().getUser(context.getUserId());
        userTLVector.add(um.toUser(context.getApiLayer()));
        UserModel umc = UserStore.getInstance().increment_pts_getUser(sc.admin_id, 1, 1, 0);
        userTLVector.add(umc.toUser(context.getApiLayer()));
        TLVector<TLUpdate> updateTLVector = new TLVector<>();
        updateTLVector.add(updateEncryption);
        TLVector<TLChat> chatTLVector = new TLVector<>();

        Updates updates = new Updates(updateTLVector, userTLVector, chatTLVector, date, 0);
        Router.getInstance().Route(sc.admin_id, updates, false);

        return encryptedChat;
    }
}