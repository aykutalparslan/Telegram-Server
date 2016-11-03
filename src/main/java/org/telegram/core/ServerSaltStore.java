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

import org.telegram.data.DatabaseConnection;
import org.telegram.data.HazelcastConnection;
import org.telegram.data.ServerSaltModel;
import org.telegram.mtproto.Utilities;

import java.security.SecureRandom;
import java.util.concurrent.ConcurrentMap;

/**
 * Created by aykut on 06/11/15.
 */
public class ServerSaltStore {
    private ConcurrentMap<Long, ServerSaltModel[]> serverSaltsShared = HazelcastConnection.getInstance().getMap("telegram_server_salts");

    private static ServerSaltStore _instance;

    private ServerSaltStore() {

    }

    public static ServerSaltStore getInstance() {
        if (_instance == null) {
            _instance = new ServerSaltStore();
        }
        return _instance;
    }

    public long getServerSalt(long authKeyId) {
        ServerSaltModel[] salts = serverSaltsShared.get(authKeyId);
        if (salts == null || salts.length == 0) {
            salts = DatabaseConnection.getInstance().getserverSalts(authKeyId, 64);
            serverSaltsShared.put(authKeyId, salts);
        }
        long salt = 0;
        for (int i = 0; i < salts.length; i++) {
            if (salts[i].validSince + 86400000L > System.currentTimeMillis()) {
                salt = salts[i].salt;
            }
        }
        if (salt == 0) {
            salts = DatabaseConnection.getInstance().getserverSalts(authKeyId, 64);
            serverSaltsShared.put(authKeyId, salts);
            for (int i = 0; i < salts.length; i++) {
                if (salts[i].validSince + 86400000L > System.currentTimeMillis()) {
                    salt = salts[i].salt;
                }
            }
        }
        if (salt == 0) {
            byte[] serverSaltBytes = new byte[8];
            long time_salt = System.currentTimeMillis();
            SecureRandom srnd = new SecureRandom();
            for (int i = 0; i < 64; i++) {
                srnd.nextBytes(serverSaltBytes);
                DatabaseConnection.getInstance().saveServerSalt(authKeyId, time_salt + ((i + 1) * 86400000L), Utilities.bytesToLong(serverSaltBytes), (i + 1) * 86400);
            }
            salts = DatabaseConnection.getInstance().getserverSalts(authKeyId, 64);
            serverSaltsShared.put(authKeyId, salts);
            for (int i = 0; i < salts.length; i++) {
                if (salts[i].validSince + 86400000L > System.currentTimeMillis()) {
                    salt = salts[i].salt;
                }
            }
        }

        return salt;
    }

    public ServerSaltModel[] getServerSalts(long authKeyId, int count) {
        ServerSaltModel[] salts = serverSaltsShared.get(authKeyId);
        if (salts == null || salts.length == 0) {
            salts = DatabaseConnection.getInstance().getserverSalts(authKeyId, 64);
            serverSaltsShared.put(authKeyId, salts);
        }
        if (salts == null || salts.length == 00) {
            byte[] serverSaltBytes = new byte[8];
            long time_salt = System.currentTimeMillis();
            SecureRandom srnd = new SecureRandom();
            for (int i = 0; i < 64; i++) {
                srnd.nextBytes(serverSaltBytes);
                DatabaseConnection.getInstance().saveServerSalt(authKeyId, time_salt + ((i + 1) * 86400000L), Utilities.bytesToLong(serverSaltBytes), (i + 1) * 86400);
            }
            salts = DatabaseConnection.getInstance().getserverSalts(authKeyId, 64);
            serverSaltsShared.put(authKeyId, salts);
        }

        return salts;
    }
}
