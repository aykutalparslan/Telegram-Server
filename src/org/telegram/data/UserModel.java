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

package org.telegram.data;

import org.telegram.tl.*;

import java.io.Serializable;

/**
 * Created by aykut on 09/11/15.
 */
public class UserModel implements Serializable {
    public int user_id;
    public String first_name;
    public String last_name;
    public String username;
    public long access_hash;
    public String phone;
    public TLUserStatus status;
    public int pts;
    public int qts;
    public int sent_messages;
    public int received_messages;

    public UserModel() {
        status = new UserStatusEmpty();
    }

    /*public UserSelf toUserSelf() {
        return new UserSelf(user_id, first_name, last_name, username, phone, new UserProfilePhotoEmpty(), status, false);
    }

    public UserContact toUserContact() {
        return new UserContact(user_id, first_name, last_name, username, access_hash, phone, new UserProfilePhotoEmpty(), status);
    }*/

    public TLUser toUser(int api_layer) {
        int flags = 0x00000001;
        flags |= 0x00000002;
        flags |= 0x00000004;
        flags |= 0x00000008;
        flags |= 0x00000010;
        flags |= 0x00000020;
        flags |= 0x00000040;
        if (api_layer >= 45) {
            return new UserL45(flags, user_id, access_hash, first_name, last_name, username, phone, new UserProfilePhotoEmpty(), status, 0, "", "");
        } else {
            return new User(flags, user_id, access_hash, first_name, last_name, username, phone, new UserProfilePhotoEmpty(), status, 0);
        }

    }

    public PeerUser toPeerUser() {
        return new PeerUser(user_id);
    }
}
