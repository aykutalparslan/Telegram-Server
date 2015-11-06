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
        //session.execute("DROP TABLE telegram.sessions;");
        //session.execute("DROP TABLE telegram.server_salts;");
        //session.execute("DROP KEYSPACE telegram;");

        session.execute("CREATE KEYSPACE IF NOT EXISTS telegram WITH replication " +
                "= {'class':'SimpleStrategy', 'replication_factor':1};");
        session.execute(
                "CREATE TABLE IF NOT EXISTS telegram.sessions (" +
                        "auth_key_id bigint PRIMARY KEY," +
                        "auth_key blob," +
                        ");");
        session.execute(
                "CREATE TABLE IF NOT EXISTS telegram.server_salts (" +
                        "auth_key_id bigint," +
                        "valid_since timestamp," +
                        "server_salt bigint," +
                        "PRIMARY KEY (auth_key_id, valid_since));");
    }

    public void saveAuthKey(long auth_key_id, byte[] auth_key){
        session.execute("INSERT INTO telegram.sessions (auth_key_id, auth_key) VALUES (?,?);",
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
        ResultSet results =  session.execute("SELECT * FROM telegram.sessions WHERE auth_key_id = ?;",
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
            ServerSaltModel s = new ServerSaltModel();
            s.salt = results.one().getLong("server_salt");
            s.validSince = results.one().getLong("valid_since");
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
