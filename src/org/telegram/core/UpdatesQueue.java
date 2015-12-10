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
import org.telegram.data.HazelcastConnection;
import org.telegram.data.UserModel;
import org.telegram.tl.TLMessageEntity;
import org.telegram.tl.TLUpdates;
import org.telegram.tl.TLVector;
import org.telegram.tl.UpdateShortMessage;

import java.util.ArrayList;

/**
 * Created by aykut on 26/11/15.
 */
public class UpdatesQueue {

    private static UpdatesQueue _instance;

    private UpdatesQueue() {
    }

    public static UpdatesQueue getInstance() {
        if (_instance == null) {
            _instance = new UpdatesQueue();
        }
        return _instance;
    }

    public TLUpdates sendMessage(int to_user_id, int from_user_id, String message, TLVector<TLMessageEntity> entities) {
        int date = (int) (System.currentTimeMillis() / 1000L);
        int msg_id;

        UserModel um = UserStore.getInstance().increment_pts_getUser(to_user_id, 1, 0, 1);
        msg_id = um.sent_messages + um.received_messages + 1;

        int flags_msg = 1;
        UpdateShortMessage msg = new UpdateShortMessage(flags_msg, msg_id,
                from_user_id, message, um.pts, 1,
                date, 0, 0, 0, entities);

        Router.getInstance().Route(to_user_id, msg, false);

        return msg;
    }
}
