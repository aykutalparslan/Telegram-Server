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

import com.hazelcast.core.IAtomicLong;
import com.hazelcast.core.IMap;
import org.telegram.data.DatabaseConnection;
import org.telegram.data.HazelcastConnection;
import org.telegram.data.UserModel;
import org.telegram.tl.TLUserStatus;

/**
 * Created by aykut on 09/11/15.
 */
public class UserStore {
    private IMap<Integer, UserModel> usersShared;
    private IMap<String, Integer> userPhoneToId;
    private IMap<String, Integer> usernameToId;
    private DatabaseConnection db;
    IAtomicLong userId;

    private static UserStore _instance;

    private UserStore() {
        db = DatabaseConnection.getInstance();
        usersShared = HazelcastConnection.getInstance().getMap("telegram_users");
        userPhoneToId = HazelcastConnection.getInstance().getMap("telegram_users_p2i");
        usernameToId = HazelcastConnection.getInstance().getMap("telegram_users_n2i");
        userId = HazelcastConnection.getInstance().getAtomicLong("user_id");
        userId.compareAndSet(0, db.getLastUserId());
    }

    public static UserStore getInstance() {
        if (_instance == null) {
            _instance = new UserStore();
        }
        return _instance;
    }

    public UserModel getUser(int user_id) {
        UserModel user = usersShared.get(user_id);
        if (user == null) {
            user = db.getUser(user_id);
            if (user != null) {
                usersShared.set(user_id, user);
                if (user.phone != null) {
                    userPhoneToId.set(user.phone, user_id);
                }
                if (user.username != null) {
                    usernameToId.set(user.username, user_id);
                }

            }
        }
        return user;
    }

    public UserModel getUser(String phone) {
        Integer user_id = userPhoneToId.get(phone);
        UserModel user = null;
        if (user_id == null || user_id == 0) {
            user = db.getUser(phone);
            if (user != null) {
                usersShared.set(user.user_id, user);
                userPhoneToId.set(user.phone, user.user_id);
                usernameToId.set(user.username, user.user_id);
                user_id = user.user_id;
            }
        } else {
            user = usersShared.get(user_id);
            if (user == null) {
                user = db.getUser(user_id);
                if (user != null) {
                    usersShared.put(user_id, user);
                }
            }
        }
        return user;
    }

    public UserModel createUser(UserModel userModel) {
        userModel.user_id = (int) userId.incrementAndGet();
        db.saveUser(userModel.user_id, userModel.first_name,
                userModel.last_name, userModel.username, userModel.access_hash, userModel.phone, 0, 0, 0);

        return getUser(userModel.user_id);
    }

    public UserModel replaceUser(UserModel userModel) {
        db.updateUser(userModel.user_id, userModel.first_name,
                userModel.last_name, userModel.username, userModel.access_hash);
        usersShared.replace(userModel.user_id, userModel);

        return getUser(userModel.user_id);
    }

    public void updateUserStatus(int user_id, TLUserStatus status) {
        if (user_id == 0) {
            return;
        }
        UserModel um = getUser(user_id);
        if (um == null) {
            return;
        }
        um.status = status;
        usersShared.replace(user_id, um);
    }

    public UserModel increment_pts_getUser(int user_id, int pts_inc, int sent_message_inc, int received_message_inc) {
        UserModel user = usersShared.get(user_id);
        if (user == null) {
            user = db.getUser(user_id);
            if (user != null) {
                usersShared.set(user_id, user);
                userPhoneToId.set(user.phone, user_id);
                usernameToId.set(user.username, user_id);
            }
        }
        if (user == null) {
            return null;
        }
        user.pts += pts_inc;
        user.sent_messages += sent_message_inc;
        user.received_messages += received_message_inc;

        usersShared.set(user_id, user);
        db.updateUser_pts(user_id, user.pts, user.sent_messages, user.received_messages);

        return user;
    }

    public UserModel decement_pts_getUser(int user_id, int pts_dec) {
        UserModel user = usersShared.get(user_id);
        if (user == null) {
            user = db.getUser(user_id);
            if (user != null) {
                usersShared.set(user_id, user);
                userPhoneToId.set(user.phone, user_id);
                usernameToId.set(user.username, user_id);
            }
        }
        if (user == null) {
            return null;
        }
        user.pts -= pts_dec;

        usersShared.set(user_id, user);
        db.updateUser_pts(user_id, user.pts, user.sent_messages, user.received_messages);

        return user;
    }

    public UserModel increment_qts_getUser(int user_id, int qts_inc) {
        UserModel user = usersShared.get(user_id);
        if (user == null) {
            user = db.getUser(user_id);
            if (user != null) {
                usersShared.set(user_id, user);
                userPhoneToId.set(user.phone, user_id);
                usernameToId.set(user.username, user_id);
            }
        }
        user.qts += qts_inc;

        usersShared.set(user_id, user);
        db.updateUser_qts(user_id, user.qts);


        return user;
    }
}
