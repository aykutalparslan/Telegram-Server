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

import com.hazelcast.core.IMap;
import org.telegram.data.AuthKeyModel;
import org.telegram.data.DatabaseConnection;
import org.telegram.data.HazelcastConnection;

/**
 * Created by aykut on 06/11/15.
 */
public class AuthKeyStore {
    private IMap<Long, AuthKeyModel> authKeysShared = HazelcastConnection.getInstance().getMap("telegram_auth_keys");

    private static AuthKeyStore _instance;

    private AuthKeyStore() {

    }

    public static AuthKeyStore getInstance() {
        if (_instance == null) {
            _instance = new AuthKeyStore();
        }
        return _instance;
    }

    public AuthKeyModel getAuthKey(long authKeyId) {
        AuthKeyModel authKey = authKeysShared.get(authKeyId);
        if (authKey == null || authKey.auth_key_id == 0) {
            authKey = DatabaseConnection.getInstance().getAuthKey(authKeyId);
            authKeysShared.set(authKeyId, authKey);
        }
        return authKey;
    }

    public void updateAuthKey(long authKeyId, String phone, int user_id) {
        DatabaseConnection.getInstance().savePhoneAndUserId(authKeyId, phone, user_id);
        AuthKeyModel authKey = DatabaseConnection.getInstance().getAuthKey(authKeyId);
        authKeysShared.set(authKeyId, authKey);
    }

    public void updateApiLayer(long authKeyId, int api_layer) {
        DatabaseConnection.getInstance().saveApiLayer(authKeyId, api_layer);
        AuthKeyModel authKey = DatabaseConnection.getInstance().getAuthKey(authKeyId);
        authKeysShared.set(authKeyId, authKey);
    }
}
