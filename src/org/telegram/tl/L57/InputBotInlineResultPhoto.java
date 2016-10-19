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

public class InputBotInlineResultPhoto extends TLInputBotInlineResult {

    public static final int ID = 0xa8d864a7;

    public String id;
    public String type;
    public TLInputPhoto photo;
    public TLInputBotInlineMessage send_message;

    public InputBotInlineResultPhoto() {
    }

    public InputBotInlineResultPhoto(String id, String type, TLInputPhoto photo, TLInputBotInlineMessage send_message) {
        this.id = id;
        this.type = type;
        this.photo = photo;
        this.send_message = send_message;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readString();
        type = buffer.readString();
        photo = (TLInputPhoto) buffer.readTLObject(APIContext.getInstance());
        send_message = (TLInputBotInlineMessage) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeString(id);
        buff.writeString(type);
        buff.writeTLObject(photo);
        buff.writeTLObject(send_message);
    }


    public int getConstructor() {
        return ID;
    }
}