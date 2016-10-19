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

package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;
import org.telegram.tl.updates.*;

public class PeerDialogs extends org.telegram.tl.messages.TLPeerDialogs {

    public static final int ID = 0x3371c354;

    public TLVector<org.telegram.tl.TLDialog> dialogs;
    public TLVector<org.telegram.tl.TLMessage> messages;
    public TLVector<org.telegram.tl.TLChat> chats;
    public TLVector<org.telegram.tl.TLUser> users;
    public TLState state;

    public PeerDialogs() {
        this.dialogs = new TLVector<>();
        this.messages = new TLVector<>();
        this.chats = new TLVector<>();
        this.users = new TLVector<>();
    }

    public PeerDialogs(TLVector<org.telegram.tl.TLDialog> dialogs, TLVector<org.telegram.tl.TLMessage> messages, TLVector<org.telegram.tl.TLChat> chats, TLVector<org.telegram.tl.TLUser> users, TLState state) {
        this.dialogs = dialogs;
        this.messages = messages;
        this.chats = chats;
        this.users = users;
        this.state = state;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        dialogs = (TLVector<org.telegram.tl.TLDialog>) buffer.readTLObject(APIContext.getInstance());
        messages = (TLVector<org.telegram.tl.TLMessage>) buffer.readTLObject(APIContext.getInstance());
        chats = (TLVector<org.telegram.tl.TLChat>) buffer.readTLObject(APIContext.getInstance());
        users = (TLVector<org.telegram.tl.TLUser>) buffer.readTLObject(APIContext.getInstance());
        state = (TLState) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(44);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeTLObject(dialogs);
        buff.writeTLObject(messages);
        buff.writeTLObject(chats);
        buff.writeTLObject(users);
        buff.writeTLObject(state);
    }


    public int getConstructor() {
        return ID;
    }
}