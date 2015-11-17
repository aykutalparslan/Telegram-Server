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

import com.hazelcast.core.IMap;
import com.hazelcast.query.SqlPredicate;
import org.telegram.data.HazelcastConnection;

/**
 * Created by aykut on 17/11/15.
 */
public class Router {
    IMap<Long, ActiveSession> activeSessions;

    private static Router _instance;

    private Router() {
        activeSessions = HazelcastConnection.getInstance().getMap("telegram_active_sessions");
        activeSessions.addIndex("phone", true);
        //activeSessions.addIndex("user_id", true);
        //activeSessions.addIndex("username", true);
    }

    public static Router getInstance() {
        if (_instance == null) {
            _instance = new Router();
        }
        return _instance;
    }

    public ActiveSession[] getActiveSessions(String phone) {
        return (ActiveSession[]) activeSessions.values(new SqlPredicate("phone = " + phone)).toArray();
    }

    public void addActiveSession(ActiveSession session) {
        activeSessions.set(session.session_id, session);
    }

    public ActiveSession getActiveSession(long session_id) {
        return activeSessions.get(session_id);
    }

    public void removeActiveSession(long session_id) {
        activeSessions.delete(session_id);
    }
}
