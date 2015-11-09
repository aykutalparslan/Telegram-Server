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

package org.telegram.api;

import com.hazelcast.core.Hazelcast;
import com.hazelcast.core.HazelcastInstance;
import org.telegram.data.DatabaseConnection;
import org.telegram.data.HazelcastConnection;
import org.telegram.data.UserModel;

import java.util.HashMap;
import java.util.concurrent.ConcurrentMap;

/**
 * Created by aykut on 09/11/15.
 */
public class UserStore {
    private ConcurrentMap<String, UserModel> usersShared = HazelcastConnection.getInstance().getMap("telegram_users");

    private static UserStore _instance;

    private UserStore() {

    }

    public static UserStore getInstance() {
        if (_instance == null) {
            _instance = new UserStore();
        }
        return _instance;
    }

    public UserModel getUser(String phone) {
        UserModel user = usersShared.get(phone);
        if (user == null) {
            user = DatabaseConnection.getInstance().getUser(phone);
            if (user != null) {
                usersShared.put(phone, user);
            }
        }
        return user;
    }

    public UserModel putUser(UserModel userModel) {
        DatabaseConnection.getInstance().saveUser(userModel.user_id, userModel.first_name,
                userModel.last_name, userModel.username, userModel.access_hash, userModel.phone);
        return getUser(userModel.phone);
    }
}
