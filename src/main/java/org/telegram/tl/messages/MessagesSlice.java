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

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class MessagesSlice extends TLMessages {

    public static final int ID = 189033187;

    public int count;
    public TLVector<TLMessage> messages;
    public TLVector<TLChat> chats;
    public TLVector<TLUser> users;

    public MessagesSlice() {
        this.messages = new TLVector<>();
        this.chats = new TLVector<>();
        this.users = new TLVector<>();
    }

    public MessagesSlice(int count, TLVector<TLMessage> messages, TLVector<TLChat> chats, TLVector<TLUser> users){
        this.count = count;
        this.messages = messages;
        this.chats = chats;
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        count = buffer.readInt();
        messages = (TLVector<TLMessage>) buffer.readTLObject(APIContext.getInstance());
        chats = (TLVector<TLChat>) buffer.readTLObject(APIContext.getInstance());
        users = (TLVector<TLUser>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeInt(count);
        buff.writeTLObject(messages);
        buff.writeTLObject(chats);
        buff.writeTLObject(users);
    }

    public int getConstructor() {
        return ID;
    }
}