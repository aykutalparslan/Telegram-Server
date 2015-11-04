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

import org.telegram.data.DatabaseConnection;
import org.telegram.data.ServerSaltModel;

import java.util.HashMap;

/**
 * Created by aykut on 04/11/15.
 */
public class Service {
    private static HashMap<Long, ServerSaltModel[]>  serverSalts = new HashMap<>();

    public static ServerSaltModel[] getServerSalts(long authKeyId, int count){
        ServerSaltModel[] salts = serverSalts.get(authKeyId);
        if(salts == null){
            salts = DatabaseConnection.getInstance().getserverSalts(authKeyId, count);
            serverSalts.put(authKeyId, salts);
        }

        return salts;
    }
}
