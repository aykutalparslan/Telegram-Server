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

import org.telegram.data.HazelcastConnection;
import org.telegram.mtproto.MTProtoAuth;

import java.util.concurrent.ConcurrentMap;

/**
 * Created by aykut on 09/11/15.
 */
public class ProtoAuthStore {
    private ConcurrentMap<byte[], MTProtoAuth> protoAuthShared = HazelcastConnection.getInstance().getMap("telegram_proto_auth");
    private static ProtoAuthStore _instance;

    private ProtoAuthStore() {

    }

    public static ProtoAuthStore getInstance() {
        if (_instance == null) {
            _instance = new ProtoAuthStore();
        }
        return _instance;
    }

    public MTProtoAuth getProtoAuth(byte[] nonce) {
        MTProtoAuth auth = protoAuthShared.get(nonce);
        if (auth == null) {
            auth = new MTProtoAuth();
            protoAuthShared.putIfAbsent(nonce, auth);
        }
        return auth;
    }

    public void updateProtoAuth(byte[] nonce, MTProtoAuth auth) {
        protoAuthShared.replace(nonce, auth);
    }

    public void removeProtoAuth(byte[] nonce) {
        protoAuthShared.remove(nonce);
    }
}
