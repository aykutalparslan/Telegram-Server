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
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.mtproto.ServerSalt;
import org.telegram.server.ServerConfig;
import org.telegram.tl.*;

import java.nio.ByteBuffer;
import java.util.ArrayList;
import java.util.List;
import java.util.Random;

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
        //session.execute("DROP TABLE telegram.auth_keys;");
        //session.execute("DROP MATERIALIZED VIEW telegram.sessions_by_user;");
        //session.execute("DROP TABLE telegram.sessions;");
        //session.execute("DROP TABLE telegram.server_salts;");
        //session.execute("DROP MATERIALIZED VIEW telegram.users_by_phone;");
        //session.execute("DROP MATERIALIZED VIEW telegram.users_by_username;");
        //session.execute("DROP TABLE telegram.users;");
        //session.execute("DROP KEYSPACE telegram;");
        //session.execute("DROP TABLE telegramfs.files;");
        //session.execute("DROP TABLE telegramfs.file_parts;");
        //session.execute("DROP KEYSPACE telegramfs;");
        //session.execute("DROP MATERIALIZED VIEW telegram.incoming_messages_by_chat;");
        //session.execute("DROP MATERIALIZED VIEW telegram.outgoing_messages_by_chat;");

        session.execute("CREATE KEYSPACE IF NOT EXISTS telegram WITH replication " +
                "= {'class':'SimpleStrategy', 'replication_factor':1};");
        session.execute(
                "CREATE TABLE IF NOT EXISTS telegram.auth_keys (" +
                        "auth_key_id bigint," +
                        "auth_key blob," +
                        "phone text," +
                        "user_id int," +
                        "api_layer int," +
                        "PRIMARY KEY (auth_key_id));");
        session.execute(
                "CREATE TABLE IF NOT EXISTS telegram.server_salts (" +
                        "auth_key_id bigint," +
                        "valid_since timestamp," +
                        "server_salt bigint," +
                        "PRIMARY KEY (auth_key_id, valid_since)) WITH CLUSTERING ORDER BY (valid_since ASC);");
        session.execute(
                "CREATE TABLE IF NOT EXISTS telegram.sessions (" +
                        "user_id int," +
                        "session_id bigint," +
                        "phone text," +
                        "auth_key_id bigint," +
                        "layer int," +
                        "PRIMARY KEY (session_id));");
        session.execute(
                "CREATE MATERIALIZED VIEW IF NOT EXISTS telegram.sessions_by_user AS " +
                        "SELECT * FROM telegram.sessions " +
                        "WHERE user_id IS NOT NULL AND session_id IS NOT NULL " +
                        "PRIMARY KEY (user_id, session_id);");
        session.execute(
                "CREATE TABLE IF NOT EXISTS telegram.users (" +
                        "user_id int," +
                        "first_name text," +
                        "last_name text," +
                        "username text," +
                        "access_hash bigint," +
                        "phone text," +
                        "pts int," +
                        "qts int," +
                        "sent_messages int," +
                        "received_messages int," +
                        "PRIMARY KEY (user_id));");
        session.execute(
                "CREATE TABLE IF NOT EXISTS telegram.profile_photos (" +
                        "user_id int," +
                        "file_id bigint," +
                        "caption text," +
                        "lat double," +
                        "lon double," +
                        "crop_left double," +
                        "crop_top double," +
                        "crop_width double," +
                        "date int," +
                        "PRIMARY KEY (user_id));");
        session.execute(
                "CREATE MATERIALIZED VIEW IF NOT EXISTS telegram.users_by_phone AS " +
                        "SELECT * FROM telegram.users " +
                        "WHERE phone IS NOT NULL AND user_id IS NOT NULL " +
                        "PRIMARY KEY (phone, user_id);");
        session.execute(
                "CREATE MATERIALIZED VIEW IF NOT EXISTS telegram.users_by_username AS " +
                        "SELECT * FROM telegram.users " +
                        "WHERE username IS NOT NULL AND user_id IS NOT NULL " +
                        "PRIMARY KEY (username, user_id);");
        session.execute(
                "CREATE TABLE IF NOT EXISTS telegram.session_queue (" +
                        "session_id bigint," +
                        "message_id bigint," +
                        "ack_received boolean," +
                        "tl_object blob," +
                        "PRIMARY KEY (session_id, message_id));");
        session.execute(
                "CREATE TABLE IF NOT EXISTS telegram.incoming_messages (" +
                        "user_id int," +
                        "from_user_id int," +
                        "to_chat_id int," +
                        "message_id int," +
                        "peer_message_id int," +
                        "message text," +
                        "media blob," +
                        "flags int," +
                        "date int," +
                        "fwd_from_id int," +
                        "fwd_date int," +
                        "reply_to_msg_id int," +
                        "entities blob," +
                        "PRIMARY KEY (user_id, from_user_id, message_id));");
        session.execute(
                "CREATE MATERIALIZED VIEW IF NOT EXISTS telegram.incoming_messages_by_chat AS " +
                        "SELECT * FROM telegram.incoming_messages " +
                        "WHERE user_id IS NOT NULL AND from_user_id IS NOT NULL AND to_chat_id IS NOT NULL AND message_id IS NOT NULL " +
                        "PRIMARY KEY (user_id, to_chat_id, from_user_id, message_id);");
        session.execute(
                "CREATE TABLE IF NOT EXISTS telegram.outgoing_messages (" +
                        "user_id int," +
                        "to_user_id int," +
                        "to_chat_id int," +
                        "message_id int," +
                        "peer_message_id int," +
                        "message text," +
                        "media blob," +
                        "flags int," +
                        "date int," +
                        "fwd_from_id int," +
                        "fwd_date int," +
                        "reply_to_msg_id int," +
                        "entities blob," +
                        "PRIMARY KEY (user_id, to_user_id, message_id));");
        session.execute(
                "CREATE MATERIALIZED VIEW IF NOT EXISTS telegram.outgoing_messages_by_chat AS " +
                        "SELECT * FROM telegram.outgoing_messages " +
                        "WHERE user_id IS NOT NULL AND to_user_id IS NOT NULL AND to_chat_id IS NOT NULL AND message_id IS NOT NULL " +
                        "PRIMARY KEY (user_id, to_chat_id, to_user_id, message_id);");
        session.execute(
                "CREATE TABLE IF NOT EXISTS telegram.chat_messages (" +
                        "to_chat_id int," +
                        "user_id int," +
                        "message text," +
                        "media blob," +
                        "date int," +
                        "fwd_from_id int," +
                        "fwd_date int," +
                        "reply_to_msg_id int," +
                        "entities blob," +
                        "PRIMARY KEY (to_chat_id));");
        session.execute(
                "CREATE TABLE IF NOT EXISTS telegram.contacts (" +
                        "user_id int," +
                        "contact_id bigint," +
                        "phone text," +
                        "first_name text," +
                        "last_name text," +
                        "is_registered boolean," +
                        "PRIMARY KEY (user_id, contact_id));");
        session.execute(
                "CREATE TABLE IF NOT EXISTS telegram.blocked_contacts (" +
                        "user_id int," +
                        "phone text," +
                        "PRIMARY KEY (user_id, phone));");
        session.execute(
                "CREATE TABLE IF NOT EXISTS telegram.chats (" +
                        "chat_id int," +
                        "admin_id int," +
                        "title text," +
                        "photo bigint," +
                        "date int," +
                        "version int," +
                        "PRIMARY KEY (chat_id));");
        session.execute(
                "CREATE TABLE IF NOT EXISTS telegram.chat_users (" +
                        "chat_id int," +
                        "user_id int," +
                        "PRIMARY KEY (chat_id));");
        session.execute(
                "CREATE TABLE IF NOT EXISTS telegram.secret_chats (" +
                        "chat_id int," +
                        "admin_id int," +
                        "participant_id int," +
                        "PRIMARY KEY (chat_id));");
        session.execute(
                "CREATE MATERIALIZED VIEW IF NOT EXISTS telegram.user_chats AS " +
                        "SELECT * FROM telegram.chat_users " +
                        "WHERE chat_id IS NOT NULL AND user_id IS NOT NULL " +
                        "PRIMARY KEY (user_id, chat_id);");
        session.execute(
                "CREATE TABLE IF NOT EXISTS telegram.dialogs (" +
                        "user_id int," +
                        "dialog_id bigint," +
                        "peer_type int," +
                        "peer_id int," +
                        "top_message int," +
                        "unread_count int," +
                        "mute_until int," +
                        "sound text," +
                        "show_previews boolean," +
                        "events_mask int," +
                        "PRIMARY KEY (user_id, dialog_id));");
        session.execute("CREATE KEYSPACE IF NOT EXISTS telegramfs WITH replication " +
                "= {'class':'SimpleStrategy', 'replication_factor':1};");
        session.execute(
                "CREATE TABLE IF NOT EXISTS telegramfs.files (" +
                        "file_id bigint," +
                        "type int," +
                        "mtime int," +
                        "part_size int," +
                        "name text," +
                        "md5_checksum text," +
                        "PRIMARY KEY (file_id));");
        session.execute(
                "CREATE TABLE IF NOT EXISTS telegramfs.file_parts (" +
                        "file_id bigint," +
                        "part_num int," +
                        "bytes blob," +
                        "size int," +
                        "PRIMARY KEY (file_id, part_num)) WITH CLUSTERING ORDER BY (part_num ASC);");
    }

    public TLChat getChat(int chat_id) {
        if (chat_id == 0) {
            return new ChatEmpty();
        }
        ResultSet results = session.execute("SELECT * FROM telegram.chats WHERE chat_id = ?;",
                chat_id);

        Chat chat = null;
        for (Row row : results) {
            chat = new Chat();
            chat.id = row.getInt("chat_id");
            chat.title = row.getString("title");
            long photo = row.getLong("photo");
            if (photo == 0) {
                chat.photo = new ChatPhotoEmpty();
            } else {
                Random rnd = new Random();
                chat.photo = new ChatPhoto(new FileLocation(ServerConfig.SERVER_ID, photo, rnd.nextInt(), photo),
                        new FileLocation(ServerConfig.SERVER_ID, photo, rnd.nextInt(), photo));
            }
            chat.date = row.getInt("date");
            chat.version = row.getInt("version");
            chat._admin_id = row.getInt("admin_id");
        }

        ResultSet results_users = session.execute("SELECT * FROM telegram.chat_users WHERE chat_id = ?;",
                chat_id);
        for (Row row : results_users) {
            chat.participants_count++;
        }

        return chat;
    }

    public int[] getChatParticipants(int chat_id) {
        if (chat_id == 0) {
            return null;
        }
        ResultSet results_users = session.execute("SELECT * FROM telegram.chat_users WHERE chat_id = ?;",
                chat_id);
        ArrayList<Integer> chat_participants = new ArrayList<>();
        for (Row row : results_users) {
            chat_participants.add(row.getInt("user_id"));

        }

        int[] participants = new int[chat_participants.size()];
        int i = 0;
        for (Integer user : chat_participants) {
            participants[i] = user;
            i++;
        }

        return participants;
    }

    public void createChat(int chat_id, String title, long photo, int date, int version, int[] users, int admin_id) {
        session.execute("INSERT INTO telegram.chats (chat_id, admin_id, title, photo, date, version) VALUES (?,?,?,?,?,?);",
                chat_id,
                admin_id,
                title,
                photo,
                date,
                version);
        for (int user_id : users) {
            addChatUser(chat_id, user_id);
        }
    }

    public void editChatTitle(int chat_id, String title) {
        session.execute("UPDATE telegram.chats  SET title = ? WHERE chat_id = ?;",
                title,
                chat_id);
    }

    public void editChatPhoto(int chat_id, long photo) {
        session.execute("UPDATE telegram.chats  SET photo = ? WHERE chat_id = ?;",
                photo,
                chat_id);
    }

    public void addChatUser(int chat_id, int user_id) {
        session.execute("INSERT INTO telegram.chat_users (chat_id, user_id) VALUES (?,?);",
                chat_id,
                user_id);
    }

    public void deleteChatUser(int chat_id, int user_id) {
        session.execute("DELETE FROM telegram.chat_users WHERE chat_id = ? AND user_id =  ?;",
                chat_id,
                user_id);
    }

    public void addSecretChat(int chat_id, int admin_id, int participant_id) {
        session.execute("INSERT INTO telegram.secret_chats (chat_id, admin_id, participant_id) VALUES (?,?,?);",
                chat_id,
                admin_id,
                participant_id);
    }

    public SecretChatModel getSecretChat(int chat_id) {
        if (chat_id == 0) {
            return null;
        }
        ResultSet results = session.execute("SELECT * FROM telegram.secret_chats WHERE chat_id = ?;",
                chat_id);

        Row row = results.one();

        return new SecretChatModel(chat_id, row.getInt("admin_id"), row.getInt("participant_id"));
    }

    public void saveProfilePhoto(int user_id, long file_id, String caption, double lat, double lon, double crop_left, double crop_top, double crop_width, int date) {
        session.execute("INSERT INTO telegram.profile_photos (user_id, file_id, caption, lat, lon, crop_left, crop_top, crop_width, date) VALUES (?,?,?,?,?,?,?,?,?);",
                user_id,
                file_id,
                caption,
                lat,
                lon,
                crop_left,
                crop_top,
                crop_width,
                date);
    }

    public UserProfilePhoto getProfilePhoto(int user_id) {
        ResultSet results = session.execute("SELECT * FROM telegram.profile_photos WHERE user_id = ?;",
                user_id);

        UserProfilePhoto profilePhoto = new UserProfilePhoto();
        FileLocation photoSmall = new FileLocation();
        FileLocation photoBig = new FileLocation();

        for (Row row : results) {
            profilePhoto.photo_id = row.getLong("file_id");
            photoSmall.dc_id = ServerConfig.SERVER_ID;
            photoSmall.volume_id = 0;
            photoSmall.local_id = 0;
            photoSmall.secret = profilePhoto.photo_id;
            profilePhoto.photo_small = photoSmall;
            photoBig.dc_id = ServerConfig.SERVER_ID;
            photoBig.volume_id = 0;
            photoBig.local_id = 1;
            photoBig.secret = profilePhoto.photo_id;
            profilePhoto.photo_big = photoBig;
        }
        return profilePhoto;
    }

    public void saveIncomingMessage(int user_id, int from_user_id, int to_chat_id, int message_id, int peer_message_id, String message, int flags, int date) {
        session.execute("INSERT INTO telegram.incoming_messages (user_id, from_user_id, to_chat_id, message_id, peer_message_id, message, flags, date) VALUES (?,?,?,?,?,?,?,?);",
                user_id,
                from_user_id,
                to_chat_id,
                message_id,
                peer_message_id,
                message,
                flags,
                date);
    }

    public void saveIncomingMessage(int user_id, int from_user_id, int to_chat_id, int message_id, int peer_message_id, byte[] media, int flags, int date) {
        session.execute("INSERT INTO telegram.incoming_messages (user_id, from_user_id, to_chat_id, message_id, peer_message_id, media, flags, date) VALUES (?,?,?,?,?,?,?,?);",
                user_id,
                from_user_id,
                to_chat_id,
                message_id,
                peer_message_id,
                ByteBuffer.wrap(media),
                flags,
                date);
    }

    public void deleteHistory(int user_id, int peer_id) {
        session.execute("DELETE FROM telegram.incoming_messages WHERE user_id = ? AND from_user_id = ?;",
                user_id,
                peer_id);

        session.execute("DELETE FROM telegram.outgoing_messages WHERE user_id = ? AND to_user_id = ?;",
                user_id,
                peer_id);
    }

    public void deleteMessages(int user_id, TLVector<Integer> messages) {
        for (int message_id : messages) {
            session.execute("DELETE FROM telegram.incoming_messages WHERE user_id = ? AND message_id = ?;",
                    user_id,
                    message_id);

            session.execute("DELETE FROM telegram.outgoing_messages WHERE user_id = ? AND message_id = ?;",
                    user_id,
                    message_id);
        }
    }

    public Message[] getIncomingMessages(int user_id) {
        ResultSet results = session.execute("SELECT * FROM telegram.incoming_messages WHERE user_id = ?;",
                user_id);

        Message[] messages = new Message[results.getAvailableWithoutFetching()];
        int i = 0;
        for (Row row : results) {
            //ByteBuffer buff = row.getBytes("media");
            TLPeer peer;
            int to_chat_id = row.getInt("to_chat_id");
            if (to_chat_id != 0) {
                peer = new PeerChat(to_chat_id);
            } else {
                peer = new PeerUser(user_id);
            }
            //if (buff != null) {
            //    byte[] bytes = new byte[buff.remaining()];
            //    if (buff.remaining() > 0) {
            //        buff.get(bytes);
            //    }

            //    if (bytes != null && bytes.length > 0) {
            //        TLObject media = APIContext.getInstance().deserialize(new ProtocolBuffer(bytes));
            //        Message m = new Message(row.getInt("flags"), row.getInt("message_id"), row.getInt("from_user_id"),
            //                peer, row.getInt("date"), "", (TLMessageMedia) media);
            //        messages[i] = m;
            //    }
            //} else {
                Message m = new Message(row.getInt("flags"), row.getInt("message_id"), row.getInt("from_user_id"),
                        peer, row.getInt("date"), row.getString("message"), new MessageMediaEmpty());
                messages[i] = m;
            //}
            row = null;
            i++;
        }

        results = null;

        return messages;
    }

    public Message[] getIncomingMessages(int user_id, int from_user_id, int max_id) {
        ResultSet results;
        if (max_id > 0) {
            results = session.execute("SELECT * FROM telegram.incoming_messages WHERE user_id = ? AND from_user_id = ? AND message_id < ?;",
                    user_id, from_user_id, max_id);
        } else {
            results = session.execute("SELECT * FROM telegram.incoming_messages WHERE user_id = ? AND from_user_id = ?;",
                    user_id, from_user_id);
        }

        Message[] messages = new Message[results.getAvailableWithoutFetching()];
        int i = 0;
        for (Row row : results) {
            /*ByteBuffer buff = row.getBytes("media");
            if (buff != null) {
                byte[] bytes = new byte[buff.remaining()];
                if (buff.remaining() > 0) {
                    buff.get(bytes);
                }

                if (bytes != null && bytes.length > 0) {
                    TLObject media = APIContext.getInstance().deserialize(new ProtocolBuffer(bytes));
                    Message m = new Message(row.getInt("flags"), row.getInt("message_id"), row.getInt("from_user_id"),
                            new PeerUser(user_id), row.getInt("date"), "", (TLMessageMedia) media);
                    messages[i] = m;
                }
            } else {*/
                Message m = new Message(row.getInt("flags"), row.getInt("message_id"), row.getInt("from_user_id"),
                        new PeerUser(user_id), row.getInt("date"), row.getString("message"), new MessageMediaEmpty());
                messages[i] = m;
            //}

            i++;
        }

        return messages;
    }

    public Message[] getIncomingChatMessages(int user_id, int to_chat_id, int max_id) {
        ResultSet results = session.execute("SELECT * FROM telegram.incoming_messages_by_chat WHERE user_id = ? AND to_chat_id = ?;",
                user_id, to_chat_id);

        Message[] messages = new Message[results.getAvailableWithoutFetching()];
        int i = 0;
        for (Row row : results) {
            /*ByteBuffer buff = row.getBytes("media");
            if (buff != null) {
                byte[] bytes = new byte[buff.remaining()];
                if (buff.remaining() > 0) {
                    buff.get(bytes);
                }

                if (bytes != null && bytes.length > 0) {
                    TLObject media = APIContext.getInstance().deserialize(new ProtocolBuffer(bytes));
                    Message m = new Message(row.getInt("flags"), row.getInt("message_id"), row.getInt("from_user_id"),
                            new PeerChat(to_chat_id), row.getInt("date"), "", (TLMessageMedia) media);
                    messages[i] = m;
                }
            } else {*/
                Message m = new Message(row.getInt("flags"), row.getInt("message_id"), row.getInt("from_user_id"),
                        new PeerChat(to_chat_id), row.getInt("date"), row.getString("message"), new MessageMediaEmpty());
                messages[i] = m;
            //}

            i++;
        }

        return messages;
    }

    public Message[] getOutgoingMessages(int user_id) {
        ResultSet results = session.execute("SELECT * FROM telegram.outgoing_messages WHERE user_id = ?;",
                user_id);

        Message[] messages = new Message[results.getAvailableWithoutFetching()];
        int i = 0;
        for (Row row : results) {
            //ByteBuffer buff = row.getBytes("media");
            int to_chat_id = row.getInt("to_chat_id");
            TLPeer peer;
            if (to_chat_id != 0) {
                peer = new PeerChat(to_chat_id);
            } else {
                peer = new PeerUser(row.getInt("to_user_id"));
            }
            //if (buff != null) {
            //    byte[] bytes = new byte[buff.remaining()];
            //    if (buff.remaining() > 0) {
            //        buff.get(bytes);
            //    }
            //    if (bytes != null && bytes.length > 0) {
            //        TLObject media = APIContext.getInstance().deserialize(new ProtocolBuffer(bytes));

            //        Message m = new Message(row.getInt("flags"), row.getInt("message_id"), user_id,
            //                peer, row.getInt("date"), "", (TLMessageMedia) media);
            //        messages[i] = m;
            //    }
            //} else {
                Message m = new Message(row.getInt("flags"), row.getInt("message_id"), user_id,
                        peer, row.getInt("date"), row.getString("message"), new MessageMediaEmpty());
                messages[i] = m;
            //}
            row = null;
            i++;
        }

        results = null;

        return messages;
    }

    public Message[] getOutgoingMessages(int user_id, int from_user_id, int max_id) {
        ResultSet results;
        if (max_id > 0) {
            results = session.execute("SELECT * FROM telegram.outgoing_messages WHERE user_id = ? AND to_user_id = ? AND message_id < ?;",
                    user_id, from_user_id, max_id);
        } else {
            results = session.execute("SELECT * FROM telegram.outgoing_messages WHERE user_id = ? AND to_user_id = ?;",
                    user_id, from_user_id);
        }


        Message[] messages = new Message[results.getAvailableWithoutFetching()];
        int i = 0;
        for (Row row : results) {
            /*ByteBuffer buff = row.getBytes("media");
            if (buff != null) {
                byte[] bytes = new byte[buff.remaining()];
                if (buff.remaining() > 0) {
                    buff.get(bytes);
                }
                if (bytes != null && bytes.length > 0) {
                    TLObject media = APIContext.getInstance().deserialize(new ProtocolBuffer(bytes));
                    Message m = new Message(row.getInt("flags"), row.getInt("message_id"), user_id,
                            new PeerUser(row.getInt("to_user_id")), row.getInt("date"), "", (TLMessageMedia) media);
                    messages[i] = m;
                }
            } else {*/
                Message m = new Message(row.getInt("flags"), row.getInt("message_id"), user_id,
                        new PeerUser(row.getInt("to_user_id")), row.getInt("date"), row.getString("message"), new MessageMediaEmpty());
                messages[i] = m;
            //}

            i++;
        }

        return messages;
    }

    public Message[] getOutgoingChatMessages(int user_id, int to_chat_id, int max_id) {
        ResultSet results = session.execute("SELECT * FROM telegram.outgoing_messages_by_chat WHERE user_id = ? AND to_chat_id = ?;",
                user_id, to_chat_id);

        Message[] messages = new Message[results.getAvailableWithoutFetching()];
        int i = 0;
        for (Row row : results) {
            /*ByteBuffer buff = row.getBytes("media");
            if (buff != null) {
                byte[] bytes = new byte[buff.remaining()];
                if (buff.remaining() > 0) {
                    buff.get(bytes);
                }
                if (bytes != null && bytes.length > 0) {
                    TLObject media = APIContext.getInstance().deserialize(new ProtocolBuffer(bytes));
                    Message m = new Message(row.getInt("flags"), row.getInt("message_id"), user_id,
                            new PeerChat(to_chat_id), row.getInt("date"), "", (TLMessageMedia) media);
                    messages[i] = m;
                }
            } else {*/
                Message m = new Message(row.getInt("flags"), row.getInt("message_id"), user_id,
                        new PeerChat(to_chat_id), row.getInt("date"), row.getString("message"), new MessageMediaEmpty());
                messages[i] = m;
            //}

            i++;
        }

        return messages;
    }

    public void saveOutgoingMessage(int user_id, int to_user_id, int to_chat_id, int message_id, int peer_message_id, String message, int flags, int date) {
        session.execute("INSERT INTO telegram.outgoing_messages (user_id, to_user_id, to_chat_id, message_id, peer_message_id, message, flags, date) VALUES (?,?,?,?,?,?,?,?);",
                user_id,
                to_user_id,
                to_chat_id,
                message_id,
                peer_message_id,
                message,
                flags,
                date);
    }

    public void saveOutgoingMessage(int user_id, int to_user_id, int to_chat_id, int message_id, int peer_message_id, byte[] media, int flags, int date) {
        session.execute("INSERT INTO telegram.outgoing_messages (user_id, to_user_id, to_chat_id, message_id, peer_message_id, media, flags, date) VALUES (?,?,?,?,?,?,?,?);",
                user_id,
                to_user_id,
                to_chat_id,
                message_id,
                peer_message_id,
                ByteBuffer.wrap(media),
                flags,
                date);
    }

    public void saveFile(long file_id, int part_size) {
        session.execute("INSERT INTO telegramfs.files (file_id, part_size) VALUES (?,?);",
                file_id,
                part_size);
    }

    public int getPartSizeForFile(long file_id) {
        ResultSet results = session.execute("SELECT part_size FROM telegramfs.files WHERE file_id = ?;",
                file_id);
        int size = 0;
        for (Row row : results) {
            size = row.getInt("part_size");
        }
        return size;
    }

    public void saveFilePart(long file_id, int part_num, byte[] bytes) {
        if (part_num == 0) {
            saveFile(file_id, bytes.length);
        }

        session.execute("INSERT INTO telegramfs.file_parts (file_id, part_num, bytes, size) VALUES (?,?,?,?);",
                file_id,
                part_num,
                ByteBuffer.wrap(bytes),
                bytes.length);
    }

    public byte[] getFilePart(long file_id, int part_num) {
        ResultSet results = session.execute("SELECT * FROM telegramfs.file_parts WHERE file_id = ? AND part_num = ?;",
                file_id,
                part_num);
        byte[] bytes = null;
        for (Row row : results) {
            ByteBuffer buff = row.getBytes("bytes");
            bytes = new byte[buff.remaining()];
            buff.get(bytes);
        }

        return bytes;
    }

    public int getFileSize(long file_id) {
        ResultSet results = session.execute("SELECT * FROM telegramfs.file_parts WHERE file_id = ?;",
                file_id);

        int size = 0;
        for (Row row : results) {
            size += row.getInt("size");
        }

        return size;
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

    public void saveContact(int user_id, long contact_id, String phone, String first_name, String last_name, boolean is_registered) {
        session.execute("INSERT INTO telegram.contacts (user_id, contact_id, phone, first_name, last_name, is_registered) VALUES (?,?,?,?,?,?);",
                user_id,
                contact_id,
                phone,
                first_name,
                last_name,
                is_registered);
    }

    public void deleteContact(int user_id, String phone) {
        session.execute("DELETE FROM telegram.contacts WHERE  user_id = ? AND phone = ?;",
                user_id,
                phone);
    }

    public ContactModel[] getContacts(int user_id) {
        ResultSet results = session.execute("SELECT * FROM telegram.contacts WHERE user_id = ?;", user_id);

        ContactModel contacts[] = new ContactModel[results.getAvailableWithoutFetching()];
        int i = 0;
        for (Row row : results) {
            ContactModel contactModel = new ContactModel();
            contactModel.contact_id = row.getLong("contact_id");
            contactModel.phone = row.getString("phone");
            contactModel.first_name = row.getString("first_name");
            contactModel.last_name = row.getString("last_name");
            contacts[i] = contactModel;
            i++;
        }
        return contacts;
    }

    public void blockContact(int user_id, String phone) {
        session.execute("INSERT INTO telegram.blocked_contacts (user_id, phone) VALUES (?,?);",
                user_id,
                phone);
    }

    public void unblockContact(int user_id, String phone) {
        session.execute("DELETE FROM telegram.blocked_contacts WHERE  user_id = ? AND phone = ?;",
                user_id,
                phone);
    }

    public ContactModel[] getBlockedContacts(int user_id, int offset, int limit) {
        ResultSet results = session.execute("SELECT * FROM telegram.blocked_contacts WHERE user_id = ?;", user_id);

        ContactModel contacts[] = new ContactModel[results.getAvailableWithoutFetching()];
        int i = 0;
        for (Row row : results) {
            ContactModel contactModel = new ContactModel();
            contactModel.phone = row.getString("phone");
            contacts[i] = contactModel;
            i++;
        }
        return contacts;
    }

    public void saveUser(int user_id, String first_name, String last_name, String username, long access_hash,
                         String phone, int pts, int sent_messages, int received_messages) {
        session.execute("INSERT INTO telegram.users (user_id, first_name, last_name, username, access_hash, phone, pts, sent_messages, received_messages) VALUES (?,?,?,?,?,?,?,?,?);",
                user_id,
                first_name,
                last_name,
                username,
                access_hash,
                phone,
                pts,
                sent_messages,
                received_messages);
    }

    public void updateUser(int user_id, String first_name, String last_name, String username, long access_hash) {
        session.execute("UPDATE telegram.users SET first_name = ?, last_name = ?, username = ?, access_hash = ? WHERE user_id = ?;",
                first_name,
                last_name,
                username,
                access_hash,
                user_id);
    }

    public void updateUser_pts(int user_id, int pts, int sent_messages, int received_messages) {
        session.execute("UPDATE telegram.users SET pts = ?, sent_messages = ?, received_messages = ? WHERE user_id = ?;",
                pts,
                sent_messages,
                received_messages,
                user_id);
    }

    public void updateUser_qts(int user_id, int qts) {
        session.execute("UPDATE telegram.users SET qts = ? WHERE user_id = ?;",
                qts,
                user_id);
    }

    public int getLastUserId() {
        ResultSet results = session.execute("SELECT max(user_id) FROM telegram.users;");

        int user_id = 0;
        if (results.getAvailableWithoutFetching() > 0) {
            user_id = results.one().getInt(0);
        }
        return user_id;
    }

    public int getLastChatId() {
        ResultSet results = session.execute("SELECT max(chat_id) FROM telegram.chats;");

        int chat_id = 0;
        if (results.getAvailableWithoutFetching() > 0) {
            chat_id = results.one().getInt(0);
        }
        return chat_id;
    }

    public UserModel getUser(int user_id) {
        if (user_id == 0) {
            return null;
        }
        ResultSet results = session.execute("SELECT * FROM telegram.users WHERE user_id = ?;",
                user_id);

        UserModel userModel = null;
        for (Row row : results) {
            userModel = new UserModel();
            userModel.user_id = row.getInt("user_id");
            userModel.first_name = row.getString("first_name");
            userModel.last_name = row.getString("last_name");
            userModel.username = row.getString("username");
            userModel.access_hash = row.getLong("access_hash");
            userModel.phone = row.getString("phone");
            userModel.pts = row.getInt("pts");
            userModel.qts = row.getInt("qts");
            userModel.sent_messages = row.getInt("sent_messages");
            userModel.received_messages = row.getInt("received_messages");
        }
        return userModel;
    }

    public UserModel getUserByUsername(String username) {
        ResultSet results = session.execute("SELECT * FROM telegram.users_by_phone WHERE usename = ?;",
                username);

        UserModel userModel = null;
        for (Row row : results) {
            userModel = new UserModel();
            userModel.user_id = row.getInt("user_id");
            userModel.first_name = row.getString("first_name");
            userModel.last_name = row.getString("last_name");
            userModel.username = row.getString("username");
            userModel.access_hash = row.getLong("access_hash");
            userModel.phone = row.getString("phone");
            userModel.pts = row.getInt("pts");
            userModel.qts = row.getInt("qts");
            userModel.sent_messages = row.getInt("sent_messages");
            userModel.received_messages = row.getInt("received_messages");
        }
        return userModel;
    }

    public UserModel getUser(String phone) {
        ResultSet results = session.execute("SELECT * FROM telegram.users_by_phone WHERE phone = ?;",
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
            userModel.pts = row.getInt("pts");
            userModel.qts = row.getInt("qts");
            userModel.sent_messages = row.getInt("sent_messages");
            userModel.received_messages = row.getInt("received_messages");
        }
        return userModel;
    }

    public UserModel[] getUsers() {
        ResultSet results = session.execute("SELECT * FROM telegram.users limit 100;");

        UserModel users[] = new UserModel[results.getAvailableWithoutFetching()];
        int i = 0;
        for (Row row : results) {
            UserModel userModel = new UserModel();
            userModel.user_id = row.getInt("user_id");
            userModel.first_name = row.getString("first_name");
            userModel.last_name = row.getString("last_name");
            userModel.username = row.getString("username");
            userModel.access_hash = row.getLong("access_hash");
            userModel.phone = row.getString("phone");
            users[i] = userModel;
            i++;
        }
        return users;
    }


    public void saveAuthKey(long auth_key_id, byte[] auth_key){
        session.execute("INSERT INTO telegram.auth_keys (auth_key_id, auth_key) VALUES (?,?);",
                auth_key_id,
                ByteBuffer.wrap(auth_key));
    }

    public void savePhoneAndUserId(long auth_key_id, String phone, int user_id) {
        session.execute("UPDATE telegram.auth_keys SET phone = ?, user_id = ? WHERE auth_key_id = ?;",
                phone,
                user_id,
                auth_key_id);
    }

    public void saveApiLayer(long auth_key_id, int api_layer) {
        session.execute("UPDATE telegram.auth_keys SET api_layer = ? WHERE auth_key_id = ?;",
                api_layer,
                auth_key_id);
    }

    public void saveServerSalt(long auth_key_id, long valid_since, long server_salt, int TTL){
        session.execute("INSERT INTO telegram.server_salts (auth_key_id, valid_since, server_salt) VALUES (?,?,?) USING TTL "+String.valueOf(TTL)+";",
                auth_key_id,
                valid_since,
                server_salt);
    }

    public AuthKeyModel getAuthKey(long auth_key_id) {
        ResultSet results = session.execute("SELECT * FROM telegram.auth_keys WHERE auth_key_id = ?;",
                auth_key_id);

        AuthKeyModel authKeyModel = new AuthKeyModel();
        for (Row row : results) {
            ByteBuffer buff = row.getBytes("auth_key");
            authKeyModel.auth_key = new byte[buff.remaining()];
            buff.get(authKeyModel.auth_key);
            authKeyModel.auth_key_id = auth_key_id;
            authKeyModel.phone = row.getString("phone");
            authKeyModel.user_id = row.getInt("user_id");
            authKeyModel.api_layer = row.getInt("api_layer");
        }

        return authKeyModel;
    }

    public ServerSaltModel[] getserverSalts(long auth_key_id, int count){
        ResultSet results =  session.execute("SELECT * FROM telegram.server_salts WHERE auth_key_id = ?;",
                auth_key_id);

        int final_count = Math.max(64, count);
        int size = Math.min(final_count, results.getAvailableWithoutFetching());
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
