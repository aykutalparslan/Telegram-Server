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

public class Chat extends TLChat {

    public static final int ID = 1855757255;

    public int id;
    public String title;
    public TLChatPhoto photo;
    public int participants_count;
    public int date;
    public boolean left;
    public int version;

    public int _admin_id;

    public Chat() {
    }

    public Chat(int id, String title, TLChatPhoto photo, int participants_count, int date, boolean left, int version){
        this.id = id;
        this.title = title;
        this.photo = photo;
        this.participants_count = participants_count;
        this.date = date;
        this.left = left;
        this.version = version;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readInt();
        title = buffer.readString();
        photo = (TLChatPhoto) buffer.readTLObject(APIContext.getInstance());
        participants_count = buffer.readInt();
        date = buffer.readInt();
        left = buffer.readBool();
        version = buffer.readInt();
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
        buff.writeInt(id);
        buff.writeString(title);
        buff.writeTLObject(photo);
        buff.writeInt(participants_count);
        buff.writeInt(date);
        buff.writeBool(left);
        buff.writeInt(version);
    }

    public ChatL42 toChatL42() {
        return new ChatL42(0, id, title, photo, participants_count, date, version, null);
    }

    public int getConstructor() {
        return ID;
    }
}