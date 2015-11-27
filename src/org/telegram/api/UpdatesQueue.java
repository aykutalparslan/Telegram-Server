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
import org.telegram.data.DatabaseConnection;
import org.telegram.data.HazelcastConnection;
import org.telegram.tl.TLUpdates;

import java.util.ArrayList;

/**
 * Created by aykut on 26/11/15.
 */
public class UpdatesQueue {
    public IMap<Integer, ArrayList<TLUpdates>> updatesIncoming = HazelcastConnection.getInstance().getMap("telegram_updates_incoming");
    public IMap<Integer, ArrayList<TLUpdates>> updatesOutgoing = HazelcastConnection.getInstance().getMap("telegram_updates_outgoing");

    private static UpdatesQueue _instance;

    private UpdatesQueue() {
    }

    public static UpdatesQueue getInstance() {
        if (_instance == null) {
            _instance = new UpdatesQueue();
        }
        return _instance;
    }
}
