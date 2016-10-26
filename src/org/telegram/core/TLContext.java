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

package org.telegram.core;

import org.telegram.data.AuthKeyModel;
import org.telegram.data.SessionModel;
import org.telegram.data.UserModel;

/**
 * Created by aykut on 04/11/15.
 */
public class TLContext {
    public TLContext() {
    }

    public boolean isAuthorized() {
        if (!authorized) {//temporary fix
            AuthKeyModel akm = AuthKeyStore.getInstance().getAuthKey(authKeyId);
            setApiLayer(akm.api_layer);
            SessionModel sm = SessionStore.getInstance().getSession(sessionId);
            if (sm == null) {
                sm = new SessionModel();
                sm.auth_key_id = authKeyId;
                sm.session_id = sessionId;
                sm.phone = akm.phone;
                SessionStore.getInstance().createSession(sm);
            }
            UserModel um = UserStore.getInstance().getUser(akm.user_id);
            if (um == null || um.phone == null || um.user_id == 0) {
                return authorized;
            }
            authorized = true;
            setPhone(um.phone);
            setUserId(um.user_id);

        }
        return authorized;
    }

    public void updateApiLayer(int api_layer) {
        AuthKeyStore.getInstance().updateApiLayer(authKeyId, api_layer);
        setApiLayer(api_layer);
    }

    public void setAuthorized(boolean authorized) {
        this.authorized = authorized;
    }

    private boolean authorized;
    private long authKeyId = 0;
    public void setAuthKeyId(long authKeyId){
        this.authKeyId = authKeyId;
    }
    public long getAuthKeyId(){
        return authKeyId;
    }


    private long sessionId = 0;

    public long getSessionId() {
        return sessionId;
    }

    public void setSessionId(long sessionId) {
        this.sessionId = sessionId;
    }

    public int getUserId() {
        return userId;
    }

    public void setUserId(int userId) {
        this.userId = userId;
    }

    private int userId;

    public String getPhone() {
        return phone;
    }

    public void setPhone(String phone) {
        this.phone = phone;
    }

    private String phone;

    private int apiLayer;

    public int getApiLayer() {
        return apiLayer;
    }

    public void setApiLayer(int apiLayer) {
        this.apiLayer = apiLayer;
    }
}
