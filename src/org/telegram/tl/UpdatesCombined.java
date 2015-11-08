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

package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class UpdatesCombined extends TLUpdates {

    public static final int ID = 1918567619;

    public TLVector<TLUpdate> updates;
    public TLVector<TLUser> users;
    public TLVector<TLChat> chats;
    public int date;
    public int seq_start;
    public int seq;

    public UpdatesCombined() {
        this.updates = new TLVector<>();
        this.users = new TLVector<>();
        this.chats = new TLVector<>();
    }

    public UpdatesCombined(TLVector<TLUpdate> updates, TLVector<TLUser> users, TLVector<TLChat> chats, int date, int seq_start, int seq){
        this.updates = updates;
        this.users = users;
        this.chats = chats;
        this.date = date;
        this.seq_start = seq_start;
        this.seq = seq;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        updates = (TLVector<TLUpdate>) buffer.readTLObject(APIContext.getInstance());
        users = (TLVector<TLUser>) buffer.readTLObject(APIContext.getInstance());
        chats = (TLVector<TLChat>) buffer.readTLObject(APIContext.getInstance());
        date = buffer.readInt();
        seq_start = buffer.readInt();
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
        buff.writeTLObject(updates);
        buff.writeTLObject(users);
        buff.writeTLObject(chats);
        buff.writeInt(date);
        buff.writeInt(seq_start);
        buff.writeInt(seq);
    }

    public int getConstructor() {
        return ID;
    }
}