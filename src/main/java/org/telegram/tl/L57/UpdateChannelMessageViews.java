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

public class UpdateChannelMessageViews extends org.telegram.tl.TLUpdate {

    public static final int ID = 0x98a12b4b;

    public int channel_id;
    public int id;
    public int views;

    public UpdateChannelMessageViews() {
    }

    public UpdateChannelMessageViews(int channel_id, int id, int views) {
        this.channel_id = channel_id;
        this.id = id;
        this.views = views;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        channel_id = buffer.readInt();
        id = buffer.readInt();
        views = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(16);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(channel_id);
        buff.writeInt(id);
        buff.writeInt(views);
    }


    public int getConstructor() {
        return ID;
    }
}