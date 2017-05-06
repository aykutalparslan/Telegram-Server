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
import org.telegram.data.UserModel;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;
import org.telegram.tl.EncryptedChatRequested;
import org.telegram.tl.EncryptedChatWaiting;
import org.telegram.tl.InputUser;
import org.telegram.tl.InputUserSelf;
import org.telegram.tl.L57.*;
import org.telegram.tl.UpdateEncryption;
import org.telegram.tl.Updates;

public class RequestEncryption extends TLObject implements TLMethod {

    public static final int ID = 0xf64daf43;

    public TLInputUser user_id;
    public int random_id;
    public byte[] g_a;

    public RequestEncryption() {
    }

    public RequestEncryption(TLInputUser user_id, int random_id, byte[] g_a){
        this.user_id = user_id;
        this.random_id = random_id;
        this.g_a = g_a;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        user_id = (TLInputUser) buffer.readTLObject(APIContext.getInstance());
        random_id = buffer.readInt();
        g_a = buffer.readBytes();
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
        buff.writeTLObject(user_id);
        buff.writeInt(random_id);
        buff.writeBytes(g_a);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        int date = (int) (System.currentTimeMillis() / 1000L);
        if(user_id instanceof InputUserSelf || user_id instanceof org.telegram.tl.L57.InputUserSelf){
            DatabaseConnection.getInstance().addSecretChat(random_id, context.getUserId(), context.getUserId());
        } else {
            DatabaseConnection.getInstance().addSecretChat(random_id, context.getUserId(), ((InputUser) user_id).user_id);
        }



        EncryptedChatRequested encryptedChatRequested = new EncryptedChatRequested(random_id, random_id, date, context.getUserId(), ((InputUser) user_id).user_id, g_a);
        UpdateEncryption updateEncryption = new UpdateEncryption(encryptedChatRequested, date);

        TLVector<TLUser> userTLVector = new TLVector<>();
        UserModel um = UserStore.getInstance().getUser(context.getUserId());
        userTLVector.add(um.toUser(context.getApiLayer()));
        UserModel umc = UserStore.getInstance().increment_pts_getUser(((InputUser) user_id).user_id, 1, 1, 0);
        userTLVector.add(umc.toUser(context.getApiLayer()));
        TLVector<TLUpdate> updateTLVector = new TLVector<>();
        updateTLVector.add(updateEncryption);
        TLVector<TLChat> chatTLVector = new TLVector<>();

        Updates updates = new Updates(updateTLVector, userTLVector, chatTLVector, date, 0);
        Router.getInstance().Route(((InputUser) user_id).user_id, updates, false);

        return new EncryptedChatWaiting(random_id, random_id, date, context.getUserId(), ((InputUser) user_id).user_id);
    }
}