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
import com.datastax.driver.core.*;
import org.telegram.mtproto.ServerSalt;

import java.nio.ByteBuffer;
import java.util.List;

public class DatabaseConnection {
    private Cluster cluster;
    private Session session;

    public Session getSession() {
        return this.session;
    }

    public void connect(String node) {
        cluster = Cluster.builder()
                .addContactPoint(node)
                .build();
        Metadata metadata = cluster.getMetadata();
        System.out.printf("Connected to cluster: %s\n",
                metadata.getClusterName());
        for ( Host host : metadata.getAllHosts() ) {
            System.out.printf("Datatacenter: %s; Host: %s; Rack: %s\n",
                    host.getDatacenter(), host.getAddress(), host.getRack());
        }
        session = cluster.connect();
    }

    public void createSchema() {
//        session.execute("DROP TABLE telegram.auth_keys;");
//        session.execute("DROP TABLE telegram.sessions;");
//        session.execute("DROP TABLE telegram.server_salts;");
//        session.execute("DROP TABLE telegram.users;");
//        session.execute("DROP KEYSPACE telegram;");

        session.execute("CREATE KEYSPACE IF NOT EXISTS telegram WITH replication " +
                "= {'class':'SimpleStrategy', 'replication_factor':1};");
        session.execute(
                "CREATE TABLE IF NOT EXISTS telegram.auth_keys (" +
                        "auth_key_id bigint," +
                        "auth_key blob," +
                        "PRIMARY KEY (auth_key_id));");
        session.execute(
                "CREATE TABLE IF NOT EXISTS telegram.server_salts (" +
                        "auth_key_id bigint," +
                        "valid_since timestamp," +
                        "server_salt bigint," +
                        "PRIMARY KEY (auth_key_id, valid_since));");
        session.execute(
                "CREATE TABLE IF NOT EXISTS telegram.sessions (" +
                        "session_id bigint," +
                        "auth_key_id bigint," +
                        "layer int," +
                        "phone text," +
                        "PRIMARY KEY (session_id, auth_key_id));");
        session.execute(
                "CREATE TABLE IF NOT EXISTS telegram.users (" +
                        "user_id int," +
                        "first_name text," +
                        "last_name text," +
                        "username text," +
                        "access_hash bigint," +
                        "phone text," +
                        "PRIMARY KEY (phone, username));");
    }

    public void saveSession(long auth_key_id, long session_id, int layer, String phone) {
        session.execute("INSERT INTO telegram.sessions (session_id, auth_key_id, layer, phone) VALUES (?,?,?,?);",
                session_id,
                auth_key_id,
                layer,
                phone);
    }

    public SessionModel getSession(long session_id) {
        ResultSet results = session.execute("SELECT * FROM telegram.sessions WHERE session_id = ?;",
                session_id);

        SessionModel sessionModel = null;
        for (Row row : results) {
            sessionModel = new SessionModel();
            sessionModel.session_id = row.getLong("session_id");
            sessionModel.auth_key_id = row.getLong("auth_key_id");
            sessionModel.layer = row.getInt("layer");
            sessionModel.phone = row.getString("phone");
        }
        return sessionModel;
    }

    public void saveUser(int user_id, String first_name, String last_name, String username, long access_hash, String phone) {
        session.execute("INSERT INTO telegram.users (user_id, first_name, last_name, username, access_hash, phone) VALUES (?,?,?,?,?,?);",
                user_id,
                first_name,
                last_name,
                username,
                access_hash,
                phone);
    }

    public void updateUser(String first_name, String last_name, String username, long access_hash, String phone) {
        session.execute("UPDATE telegram.users SET first_name = ?, last_name = ?, username = ?, access_hash = ? WHERE phone = ?;",
                first_name,
                last_name,
                username,
                access_hash,
                phone);
    }

    public UserModel getUser(String phone) {
        ResultSet results = session.execute("SELECT * FROM telegram.users WHERE phone = ?;",
                phone);

        UserModel userModel = null;
        for (Row row : results) {
            userModel = new UserModel();
            userModel.user_id = row.getInt("user_id");
            userModel.first_name = row.getString("first_name");
            userModel.last_name = row.getString("last_name");
            userModel.username = row.getString("username");
            userModel.access_hash = row.getLong("access_hash");
            userModel.phone = row.getString("phone");
        }
        return userModel;
    }


    public void saveAuthKey(long auth_key_id, byte[] auth_key){
        session.execute("INSERT INTO telegram.auth_keys (auth_key_id, auth_key) VALUES (?,?);",
                auth_key_id,
                ByteBuffer.wrap(auth_key));
    }

    public void saveServerSalt(long auth_key_id, long valid_since, long server_salt, int TTL){
        session.execute("INSERT INTO telegram.server_salts (auth_key_id, valid_since, server_salt) VALUES (?,?,?) USING TTL "+String.valueOf(TTL)+";",
                auth_key_id,
                valid_since,
                server_salt);
    }

    public byte[] getAuthKey(long auth_key_id){
        ResultSet results = session.execute("SELECT * FROM telegram.auth_keys WHERE auth_key_id = ?;",
                auth_key_id);

        byte[] bytes = null;
        for (Row row : results) {
            ByteBuffer buff = row.getBytes("auth_key");
            bytes = new byte[buff.remaining()];
            buff.get(bytes);
        }

        return bytes;
    }

    public ServerSaltModel[] getserverSalts(long auth_key_id, int count){
        ResultSet results =  session.execute("SELECT * FROM telegram.server_salts WHERE auth_key_id = ?;",
                auth_key_id);

        int final_count = Math.max(64, count);
        int size = Math.max(final_count, results.getAvailableWithoutFetching());
        ServerSaltModel[] salts = new ServerSaltModel[size];

        for (int i = 0; i < size; i++) {
            Row result = results.one();
            ServerSaltModel s = new ServerSaltModel();
            s.salt = result.getLong("server_salt");
            s.validSince = result.getTimestamp("valid_since").getTime();
            salts[i] = s;
        }

        return salts;
    }

    public void close() {
        session.close();
        cluster.close();
    }

    private static DatabaseConnection instance = null;
    private DatabaseConnection() {
    }
    public static DatabaseConnection getInstance() {
        if(instance == null) {
            instance = new DatabaseConnection();
            instance.connect("127.0.0.1");
            instance.createSchema();
        }
        return instance;
    }
}
