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

package org.telegram.tl.updates;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;
import org.telegram.tl.updates.*;

public class Difference extends TLDifference {

    public static final int ID = 16030880;

    public TLVector<TLMessage> new_messages;
    public TLVector<TLEncryptedMessage> new_encrypted_messages;
    public TLVector<TLUpdate> other_updates;
    public TLVector<TLChat> chats;
    public TLVector<TLUser> users;
    public TLState state;

    public Difference() {
        this.new_messages = new TLVector<>();
        this.new_encrypted_messages = new TLVector<>();
        this.other_updates = new TLVector<>();
        this.chats = new TLVector<>();
        this.users = new TLVector<>();
    }

    public Difference(TLVector<TLMessage> new_messages, TLVector<TLEncryptedMessage> new_encrypted_messages, TLVector<TLUpdate> other_updates, TLVector<TLChat> chats, TLVector<TLUser> users, TLState state){
        this.new_messages = new_messages;
        this.new_encrypted_messages = new_encrypted_messages;
        this.other_updates = other_updates;
        this.chats = chats;
        this.users = users;
        this.state = state;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        new_messages = (TLVector<TLMessage>) buffer.readTLObject(APIContext.getInstance());
        new_encrypted_messages = (TLVector<TLEncryptedMessage>) buffer.readTLObject(APIContext.getInstance());
        other_updates = (TLVector<TLUpdate>) buffer.readTLObject(APIContext.getInstance());
        chats = (TLVector<TLChat>) buffer.readTLObject(APIContext.getInstance());
        users = (TLVector<TLUser>) buffer.readTLObject(APIContext.getInstance());
        state = (TLState) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(new_messages);
        buff.writeTLObject(new_encrypted_messages);
        buff.writeTLObject(other_updates);
        buff.writeTLObject(chats);
        buff.writeTLObject(users);
        buff.writeTLObject(state);
    }

    public int getConstructor() {
        return ID;
    }
}