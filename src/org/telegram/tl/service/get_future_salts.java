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

package org.telegram.tl.service;

import org.telegram.core.ServerSaltStore;
import org.telegram.core.TLContext;
import org.telegram.core.TLMethod;
import org.telegram.data.ServerSaltModel;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class get_future_salts extends TLObject implements TLMethod{

    public static final int ID = 0xb921bd04;

    public int num;

    public get_future_salts() {

    }

    public get_future_salts(int num){
        this.num = num;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        num = buffer.readInt();
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
        buff.writeInt(num);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context,long messageId, long reqMessageId) {
        ServerSaltModel[] saltsArr = ServerSaltStore.getInstance().getServerSalts(context.getAuthKeyId(), 64);

        TLVector<future_salt> salts = new TLVector<>();
        for (int i = 0; i < saltsArr.length; i++) {
            future_salt futureSalt = new future_salt((int)(saltsArr[i].validSince / 1000), (int)((saltsArr[i].validSince / 1000) + 8600), saltsArr[i].salt);
            salts.add(futureSalt);
        }

        return new future_salts(reqMessageId, (int)(System.currentTimeMillis() / 1000), salts);
    }
}