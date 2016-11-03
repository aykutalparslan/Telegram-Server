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

public class EncryptedChatWaiting extends TLEncryptedChat {

    public static final int ID = 0x3bf703dc;

    public int id;
    public long access_hash;
    public int date;
    public int admin_id;
    public int participant_id;

    public EncryptedChatWaiting() {
    }

    public EncryptedChatWaiting(int id, long access_hash, int date, int admin_id, int participant_id){
        this.id = id;
        this.access_hash = access_hash;
        this.date = date;
        this.admin_id = admin_id;
        this.participant_id = participant_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readInt();
        access_hash = buffer.readLong();
        date = buffer.readInt();
        admin_id = buffer.readInt();
        participant_id = buffer.readInt();
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
        buff.writeLong(access_hash);
        buff.writeInt(date);
        buff.writeInt(admin_id);
        buff.writeInt(participant_id);
    }

    public int getConstructor() {
        return ID;
    }
}