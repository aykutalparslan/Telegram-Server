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

public class UpdateServiceNotification extends TLUpdate {

    public static final int ID = 942527460;

    public String type;
    public String message;
    public TLMessageMedia media;
    public boolean popup;

    public UpdateServiceNotification() {
    }

    public UpdateServiceNotification(String type, String message, TLMessageMedia media, boolean popup){
        this.type = type;
        this.message = message;
        this.media = media;
        this.popup = popup;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        type = buffer.readString();
        message = buffer.readString();
        media = (TLMessageMedia) buffer.readTLObject(APIContext.getInstance());
        popup = buffer.readBool();
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
        buff.writeString(type);
        buff.writeString(message);
        buff.writeTLObject(media);
        buff.writeBool(popup);
    }

    public int getConstructor() {
        return ID;
    }
}