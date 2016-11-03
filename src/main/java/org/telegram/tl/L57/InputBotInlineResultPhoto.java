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

public class InputBotInlineResultPhoto extends org.telegram.tl.TLInputBotInlineResult {

    public static final int ID = 0xa8d864a7;

    public String id;
    public String type;
    public org.telegram.tl.TLInputPhoto photo;
    public org.telegram.tl.TLInputBotInlineMessage send_message;

    public InputBotInlineResultPhoto() {
    }

    public InputBotInlineResultPhoto(String id, String type, org.telegram.tl.TLInputPhoto photo, org.telegram.tl.TLInputBotInlineMessage send_message) {
        this.id = id;
        this.type = type;
        this.photo = photo;
        this.send_message = send_message;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readString();
        type = buffer.readString();
        photo = (org.telegram.tl.TLInputPhoto) buffer.readTLObject(APIContext.getInstance());
        send_message = (org.telegram.tl.TLInputBotInlineMessage) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(36);
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