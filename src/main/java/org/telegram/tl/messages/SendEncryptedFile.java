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
import org.telegram.server.ServerConfig;
import org.telegram.tl.*;

public class SendEncryptedFile extends TLObject implements TLMethod {

    public static final int ID = -1701831834;

    public TLInputEncryptedChat peer;
    public long random_id;
    public byte[] data;
    public TLInputEncryptedFile file;

    public SendEncryptedFile() {
    }

    public SendEncryptedFile(TLInputEncryptedChat peer, long random_id, byte[] data, TLInputEncryptedFile file){
        this.peer = peer;
        this.random_id = random_id;
        this.data = data;
        this.file = file;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLInputEncryptedChat) buffer.readTLObject(APIContext.getInstance());
        random_id = buffer.readLong();
        data = buffer.readBytes();
        file = (TLInputEncryptedFile) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeLong(random_id);
        buff.writeBytes(data);
        buff.writeTLObject(file);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        int date = (int) (System.currentTimeMillis() / 1000L);

        int chat_id = ((InputEncryptedChat) peer).chat_id;
        SecretChatModel sc = DatabaseConnection.getInstance().getSecretChat(chat_id);

        int user_id = 0;
        if (context.getUserId() == sc.admin_id) {
            user_id = sc.participant_id;
        } else if (context.getUserId() == sc.participant_id) {
            user_id = sc.admin_id;
        }

        TLVector<TLUser> userTLVector = new TLVector<>();
        UserModel um = UserStore.getInstance().getUser(context.getUserId());
        userTLVector.add(um.toUser(context.getApiLayer()));
        UserModel umc = UserStore.getInstance().increment_qts_getUser(user_id, 1);
        userTLVector.add(umc.toUser(context.getApiLayer()));
        TLVector<TLUpdate> updateTLVector = new TLVector<>();
        EncryptedFile encryptedFile = null;
        if (file instanceof InputEncryptedFileUploaded) {
            long file_id = ((InputEncryptedFileUploaded) file).id;

            int file_size = DatabaseConnection.getInstance().getFileSize(file_id);
            encryptedFile = new EncryptedFile(file_id, ((InputEncryptedFileUploaded) file).id, file_size, ServerConfig.SERVER_ID,
                    ((InputEncryptedFileUploaded) file).key_fingerprint);
            EncryptedMessage encryptedMessage = new EncryptedMessage(random_id, chat_id, date, data,
                    encryptedFile);
            UpdateNewEncryptedMessage updateNewEncryptedMessage = new UpdateNewEncryptedMessage(encryptedMessage, umc.qts);
            updateTLVector.add(updateNewEncryptedMessage);
            TLVector<TLChat> chatTLVector = new TLVector<>();

            Updates updates = new Updates(updateTLVector, userTLVector, chatTLVector, date, 0);
            Router.getInstance().Route(user_id, updates, false);
        } else if (file instanceof InputEncryptedFileBigUploaded) {
            long file_id = ((InputEncryptedFileBigUploaded) file).id;

            int file_size = DatabaseConnection.getInstance().getFileSize(file_id);
            encryptedFile = new EncryptedFile(file_id, ((InputEncryptedFileBigUploaded) file).id, file_size, ServerConfig.SERVER_ID,
                    ((InputEncryptedFileBigUploaded) file).key_fingerprint);
            EncryptedMessage encryptedMessage = new EncryptedMessage(random_id, chat_id, date, data,
                    encryptedFile);
            UpdateNewEncryptedMessage updateNewEncryptedMessage = new UpdateNewEncryptedMessage(encryptedMessage, umc.qts);
            updateTLVector.add(updateNewEncryptedMessage);
            TLVector<TLChat> chatTLVector = new TLVector<>();

            Updates updates = new Updates(updateTLVector, userTLVector, chatTLVector, date, 0);
            Router.getInstance().Route(user_id, updates, false);
        }


        return new SentEncryptedFile(date, encryptedFile);
    }
}