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

public class EditChatPhoto extends TLObject {

    public static final int ID = -662601187;

    public int chat_id;
    public TLInputChatPhoto photo;

    public EditChatPhoto() {
    }

    public EditChatPhoto(int chat_id, TLInputChatPhoto photo){
        this.chat_id = chat_id;
        this.photo = photo;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        chat_id = buffer.readInt();
        photo = (TLInputChatPhoto) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(photo);
    }

    public int getConstructor() {
        return ID;
    }
}