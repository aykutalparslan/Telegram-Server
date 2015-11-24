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

package org.telegram.tl.account;

import org.telegram.api.TLContext;
import org.telegram.api.TLMethod;
import org.telegram.api.UserStore;
import org.telegram.data.UserModel;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class UpdateStatus extends TLObject implements TLMethod {

    public static final int ID = 1713919532;

    public boolean offline;

    public UpdateStatus() {
    }

    public UpdateStatus(boolean offline){
        this.offline = offline;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        offline = buffer.readBool();
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
        buff.writeBool(offline);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        if (offline) {
            UserStore.getInstance().updateUserStatus(context.getUserId(), new UserStatusOffline());
        } else {
            UserStore.getInstance().updateUserStatus(context.getUserId(), new UserStatusOnline());
        }

        return new BoolTrue();
    }
}