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

package org.telegram.tl.L57.updates;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;
import org.telegram.tl.updates.*;

public class DifferenceSlice extends TLDifference {

    public static final int ID = 0xa8fb1981;

    public TLVector<org.telegram.tl.TLMessage> new_messages;
    public TLVector<org.telegram.tl.TLEncryptedMessage> new_encrypted_messages;
    public TLVector<org.telegram.tl.TLUpdate> other_updates;
    public TLVector<org.telegram.tl.TLChat> chats;
    public TLVector<org.telegram.tl.TLUser> users;
    public TLState intermediate_state;

    public DifferenceSlice() {
        this.new_messages = new TLVector<>();
        this.new_encrypted_messages = new TLVector<>();
        this.other_updates = new TLVector<>();
        this.chats = new TLVector<>();
        this.users = new TLVector<>();
    }

    public DifferenceSlice(TLVector<org.telegram.tl.TLMessage> new_messages, TLVector<org.telegram.tl.TLEncryptedMessage> new_encrypted_messages, TLVector<org.telegram.tl.TLUpdate> other_updates, TLVector<org.telegram.tl.TLChat> chats, TLVector<org.telegram.tl.TLUser> users, TLState intermediate_state) {
        this.new_messages = new_messages;
        this.new_encrypted_messages = new_encrypted_messages;
        this.other_updates = other_updates;
        this.chats = chats;
        this.users = users;
        this.intermediate_state = intermediate_state;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        new_messages = (TLVector<org.telegram.tl.TLMessage>) buffer.readTLObject(APIContext.getInstance());
        new_encrypted_messages = (TLVector<org.telegram.tl.TLEncryptedMessage>) buffer.readTLObject(APIContext.getInstance());
        other_updates = (TLVector<org.telegram.tl.TLUpdate>) buffer.readTLObject(APIContext.getInstance());
        chats = (TLVector<org.telegram.tl.TLChat>) buffer.readTLObject(APIContext.getInstance());
        users = (TLVector<org.telegram.tl.TLUser>) buffer.readTLObject(APIContext.getInstance());
        intermediate_state = (TLState) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(52);
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
        buff.writeTLObject(intermediate_state);
    }


    public int getConstructor() {
        return ID;
    }
}