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

public class EncryptedMessageService extends TLEncryptedMessage {

    public static final int ID = 594758406;

    public long random_id;
    public int chat_id;
    public int date;
    public byte[] bytes;

    public EncryptedMessageService() {
    }

    public EncryptedMessageService(long random_id, int chat_id, int date, byte[] bytes){
        this.random_id = random_id;
        this.chat_id = chat_id;
        this.date = date;
        this.bytes = bytes;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        random_id = buffer.readLong();
        chat_id = buffer.readInt();
        date = buffer.readInt();
        bytes = buffer.readBytes();
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
        buff.writeLong(random_id);
        buff.writeInt(chat_id);
        buff.writeInt(date);
        buff.writeBytes(bytes);
    }

    public int getConstructor() {
        return ID;
    }
}