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
import org.telegram.tl.contacts.*;
import org.telegram.tl.contacts.*;

public class UpdateContactLink extends TLUpdate {

    public static final int ID = 1369737882;

    public int user_id;
    public TLMyLink my_link;
    public TLForeignLink foreign_link;

    public UpdateContactLink() {
    }

    public UpdateContactLink(int user_id, TLMyLink my_link, TLForeignLink foreign_link){
        this.user_id = user_id;
        this.my_link = my_link;
        this.foreign_link = foreign_link;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        user_id = buffer.readInt();
        my_link = (TLMyLink) buffer.readTLObject(APIContext.getInstance());
        foreign_link = (TLForeignLink) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeInt(user_id);
        buff.writeTLObject(my_link);
        buff.writeTLObject(foreign_link);
    }

    public int getConstructor() {
        return ID;
    }
}