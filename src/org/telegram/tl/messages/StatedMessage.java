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

public class StatedMessage extends TLStatedMessage {

    public static final int ID = -797251802;

    public TLMessage message;
    public TLVector<TLChat> chats;
    public TLVector<TLUser> users;
    public int pts;
    public int seq;

    public StatedMessage() {
        this.chats = new TLVector<>();
        this.users = new TLVector<>();
    }

    public StatedMessage(TLMessage message, TLVector<TLChat> chats, TLVector<TLUser> users, int pts, int seq){
        this.message = message;
        this.chats = chats;
        this.users = users;
        this.pts = pts;
        this.seq = seq;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        message = (TLMessage) buffer.readTLObject(APIContext.getInstance());
        chats = (TLVector<TLChat>) buffer.readTLObject(APIContext.getInstance());
        users = (TLVector<TLUser>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(message);
        buff.writeTLObject(chats);
        buff.writeTLObject(users);
        buff.writeInt(pts);
        buff.writeInt(seq);
    }

    public int getConstructor() {
        return ID;
    }
}