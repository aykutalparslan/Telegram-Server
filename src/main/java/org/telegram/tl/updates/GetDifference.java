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

package org.telegram.tl.updates;

import org.telegram.core.TLContext;
import org.telegram.core.TLMethod;
import org.telegram.core.UserStore;
import org.telegram.data.UserModel;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;
import org.telegram.tl.service.rpc_error;

public class GetDifference extends TLObject implements TLMethod {

    public static final int ID = 168039573;

    public int pts;
    public int date;
    public int qts;

    public GetDifference() {
    }

    public GetDifference(int pts, int date, int qts){
        this.pts = pts;
        this.date = date;
        this.qts = qts;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        pts = buffer.readInt();
        date = buffer.readInt();
        qts = buffer.readInt();
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
        buff.writeInt(pts);
        buff.writeInt(date);
        buff.writeInt(qts);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        if (context.isAuthorized()) {
            UserModel um = UserStore.getInstance().getUser(context.getUserId());
            return new Difference(new TLVector<TLMessage>(), new TLVector<TLEncryptedMessage>(),
                    new TLVector<TLUpdate>(), new TLVector<TLChat>(), new TLVector<TLUser>(),
                    new State(um.pts, um.qts, date, um.pts, 0));
        }

        return rpc_error.UNAUTHORIZED();
    }
}