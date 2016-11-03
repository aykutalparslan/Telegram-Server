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
import org.telegram.data.DatabaseConnection;
import org.telegram.data.HazelcastConnection;
import org.telegram.data.SessionModel;

/**
 * Created by aykut on 09/11/15.
 */
public class SessionStore {
    private IMap<Long, SessionModel> sessionsShared = HazelcastConnection.getInstance().getMap("telegram_sessions");

    private static SessionStore _instance;

    private SessionStore() {

    }

    public static SessionStore getInstance() {
        if (_instance == null) {
            _instance = new SessionStore();
        }
        return _instance;
    }

    public SessionModel getSession(long session_id) {
        SessionModel session = sessionsShared.get(session_id);
        if (session == null) {
            session = DatabaseConnection.getInstance().getSession(session_id);
            if (session != null) {
                sessionsShared.set(session_id, session);
            }
        }
        return session;
    }

    public SessionModel createSession(SessionModel sessionModel) {
        DatabaseConnection.getInstance().saveSession(sessionModel.auth_key_id, sessionModel.session_id, sessionModel.layer, sessionModel.phone);

        return getSession(sessionModel.session_id);
    }
}
