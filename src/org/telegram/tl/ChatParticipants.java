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

public class ChatParticipants extends TLChatParticipants {

    public static final int ID = 2017571861;

    public int chat_id;
    public int admin_id;
    public TLVector<TLChatParticipant> participants;
    public int version;

    public ChatParticipants() {
        this.participants = new TLVector<>();
    }

    public ChatParticipants(int chat_id, int admin_id, TLVector<TLChatParticipant> participants, int version){
        this.chat_id = chat_id;
        this.admin_id = admin_id;
        this.participants = participants;
        this.version = version;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        chat_id = buffer.readInt();
        admin_id = buffer.readInt();
        participants = (TLVector<TLChatParticipant>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeInt(chat_id);
        buff.writeInt(admin_id);
        buff.writeTLObject(participants);
        buff.writeInt(version);
    }

    public int getConstructor() {
        return ID;
    }
}