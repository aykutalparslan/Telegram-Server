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

public class UpdateChatParticipantAdd extends org.telegram.tl.TLUpdate {

    public static final int ID = 0xea4b0e5c;

    public int chat_id;
    public int user_id;
    public int inviter_id;
    public int date;
    public int version;

    public UpdateChatParticipantAdd() {
    }

    public UpdateChatParticipantAdd(int chat_id, int user_id, int inviter_id, int date, int version) {
        this.chat_id = chat_id;
        this.user_id = user_id;
        this.inviter_id = inviter_id;
        this.date = date;
        this.version = version;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        chat_id = buffer.readInt();
        user_id = buffer.readInt();
        inviter_id = buffer.readInt();
        date = buffer.readInt();
        version = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(24);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(chat_id);
        buff.writeInt(user_id);
        buff.writeInt(inviter_id);
        buff.writeInt(date);
        buff.writeInt(version);
    }


    public int getConstructor() {
        return ID;
    }
}