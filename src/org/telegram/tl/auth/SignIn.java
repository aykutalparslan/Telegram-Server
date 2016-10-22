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

package org.telegram.tl.auth;

import org.telegram.core.*;
import org.telegram.data.SessionModel;
import org.telegram.data.UserModel;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;
import org.telegram.tl.service.rpc_error;

public class SignIn extends TLObject implements TLMethod {

    public static final int ID = -1126886015;

    public String phone_number;
    public String phone_code_hash;
    public String phone_code;

    public SignIn() {
    }

    public SignIn(String phone_number, String phone_code_hash, String phone_code){
        this.phone_number = phone_number;
        this.phone_code_hash = phone_code_hash;
        this.phone_code = phone_code;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        phone_number = buffer.readString();
        phone_code_hash = buffer.readString();
        phone_code = buffer.readString();
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
        buff.writeString(phone_number);
        buff.writeString(phone_code_hash);
        buff.writeString(phone_code);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        UserModel userModel = UserStore.getInstance().getUser(clearPhone(phone_number));
        if (userModel != null) {
            SessionModel sm = SessionStore.getInstance().getSession(context.getSessionId());
            if (sm == null) {
                SessionModel sessionModel = new SessionModel();
                sessionModel.auth_key_id = context.getAuthKeyId();
                sessionModel.session_id = context.getSessionId();
                sessionModel.layer = 0;
                sessionModel.phone = clearPhone(phone_number);
                SessionStore.getInstance().createSession(sessionModel);
            }

            AuthKeyStore.getInstance().updateAuthKey(context.getAuthKeyId(), clearPhone(phone_number), userModel.user_id);

            UserStatusOnline online = new UserStatusOnline(120);
            UserStore.getInstance().updateUserStatus(userModel.user_id, online);
            if (context.getApiLayer() >= 57) {
                return new org.telegram.tl.L57.auth.Authorization(Integer.MAX_VALUE, 5, userModel.toUser(context.getApiLayer()));
            } else {
                return new Authorization(Integer.MAX_VALUE, userModel.toUser(context.getApiLayer()));
            }

        } else {
            return new rpc_error(400, "PHONE_NUMBER_UNOCCUPIED");
        }

    }

    public String clearPhone(String phone) {
        return phone.replace("(", "").replace(")", "").replace(" ", "").replace("-", "").replace("+", "");
    }
}