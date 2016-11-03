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

public class UpdateWebPage extends org.telegram.tl.TLUpdate {

    public static final int ID = 0x7f891213;

    public org.telegram.tl.TLWebPage webpage;
    public int pts;
    public int pts_count;

    public UpdateWebPage() {
    }

    public UpdateWebPage(org.telegram.tl.TLWebPage webpage, int pts, int pts_count) {
        this.webpage = webpage;
        this.pts = pts;
        this.pts_count = pts_count;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        webpage = (org.telegram.tl.TLWebPage) buffer.readTLObject(APIContext.getInstance());
        pts = buffer.readInt();
        pts_count = buffer.readInt();
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
        buff.writeTLObject(webpage);
        buff.writeInt(pts);
        buff.writeInt(pts_count);
    }


    public int getConstructor() {
        return ID;
    }
}