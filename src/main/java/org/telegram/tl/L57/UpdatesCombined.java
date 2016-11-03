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

package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdatesCombined extends org.telegram.tl.TLUpdates {

    public static final int ID = 0x725b04c3;

    public TLVector<org.telegram.tl.TLUpdate> updates;
    public TLVector<org.telegram.tl.TLUser> users;
    public TLVector<org.telegram.tl.TLChat> chats;
    public int date;
    public int seq_start;
    public int seq;

    public UpdatesCombined() {
        this.updates = new TLVector<>();
        this.users = new TLVector<>();
        this.chats = new TLVector<>();
    }

    public UpdatesCombined(TLVector<org.telegram.tl.TLUpdate> updates, TLVector<org.telegram.tl.TLUser> users, TLVector<org.telegram.tl.TLChat> chats, int date, int seq_start, int seq) {
        this.updates = updates;
        this.users = users;
        this.chats = chats;
        this.date = date;
        this.seq_start = seq_start;
        this.seq = seq;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        updates = (TLVector<org.telegram.tl.TLUpdate>) buffer.readTLObject(APIContext.getInstance());
        users = (TLVector<org.telegram.tl.TLUser>) buffer.readTLObject(APIContext.getInstance());
        chats = (TLVector<org.telegram.tl.TLChat>) buffer.readTLObject(APIContext.getInstance());
        date = buffer.readInt();
        seq_start = buffer.readInt();
        seq = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(40);
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