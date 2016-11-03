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
import org.telegram.tl.contacts.*;

public class StatedMessagesLinks extends TLStatedMessages {

    public static final int ID = 1047852486;

    public TLVector<TLMessage> messages;
    public TLVector<TLChat> chats;
    public TLVector<TLUser> users;
    public TLVector<TLLink> links;
    public int pts;
    public int seq;

    public StatedMessagesLinks() {
        this.messages = new TLVector<>();
        this.chats = new TLVector<>();
        this.users = new TLVector<>();
        this.links = new TLVector<>();
    }

    public StatedMessagesLinks(TLVector<TLMessage> messages, TLVector<TLChat> chats, TLVector<TLUser> users, TLVector<TLLink> links, int pts, int seq){
        this.messages = messages;
        this.chats = chats;
        this.users = users;
        this.links = links;
        this.pts = pts;
        this.seq = seq;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        messages = (TLVector<TLMessage>) buffer.readTLObject(APIContext.getInstance());
        chats = (TLVector<TLChat>) buffer.readTLObject(APIContext.getInstance());
        users = (TLVector<TLUser>) buffer.readTLObject(APIContext.getInstance());
        links = (TLVector<TLLink>) buffer.readTLObject(APIContext.getInstance());
        pts = buffer.readInt();
        seq = buffer.readInt();
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
        buff.writeTLObject(messages);
        buff.writeTLObject(chats);
        buff.writeTLObject(users);
        buff.writeTLObject(links);
        buff.writeInt(pts);
        buff.writeInt(seq);
    }

    public int getConstructor() {
        return ID;
    }
}