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

public class ChatParticipants extends org.telegram.tl.TLChatParticipants {

    public static final int ID = 0x3f460fed;

    public int chat_id;
    public TLVector<org.telegram.tl.TLChatParticipant> participants;
    public int version;

    public ChatParticipants() {
        this.participants = new TLVector<>();
    }

    public ChatParticipants(int chat_id, TLVector<org.telegram.tl.TLChatParticipant> participants, int version) {
        this.chat_id = chat_id;
        this.participants = participants;
        this.version = version;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        chat_id = buffer.readInt();
        participants = (TLVector<org.telegram.tl.TLChatParticipant>) buffer.readTLObject(APIContext.getInstance());
        version = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(20);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(chat_id);
        buff.writeTLObject(participants);
        buff.writeInt(version);
    }


    public int getConstructor() {
        return ID;
    }
}