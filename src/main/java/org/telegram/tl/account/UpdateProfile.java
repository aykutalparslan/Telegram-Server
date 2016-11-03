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

import org.telegram.core.SessionStore;
import org.telegram.core.TLContext;
import org.telegram.core.TLMethod;
import org.telegram.core.UserStore;
import org.telegram.data.SessionModel;
import org.telegram.data.UserModel;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class UpdateProfile extends TLObject implements TLMethod {

    public static final int ID = -259486360;

    public String first_name;
    public String last_name;

    public UpdateProfile() {
    }

    public UpdateProfile(String first_name, String last_name){
        this.first_name = first_name;
        this.last_name = last_name;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        first_name = buffer.readString();
        last_name = buffer.readString();
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
        buff.writeString(first_name);
        buff.writeString(last_name);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        SessionModel sm = SessionStore.getInstance().getSession(context.getSessionId());
        UserModel userModel = UserStore.getInstance().getUser(sm.phone);
        userModel.first_name = first_name;
        userModel.last_name = last_name;
        UserStore.getInstance().replaceUser(userModel);
        return new UserSelf(userModel.user_id, userModel.first_name, userModel.last_name,
                userModel.username, userModel.phone, new UserProfilePhotoEmpty(), new UserStatusEmpty(), false);
    }
}