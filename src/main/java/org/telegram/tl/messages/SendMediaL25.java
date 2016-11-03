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

package org.telegram.tl.messages;

import org.telegram.core.*;
import org.telegram.data.DatabaseConnection;
import org.telegram.data.UserModel;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.server.ServerConfig;
import org.telegram.tl.*;
import org.telegram.tl.service.rpc_error;

import java.util.Random;

public class SendMediaL25 extends TLObject implements TLMethod {

    public static final int ID = 0x2d7923b1;

    public int flags;
    public TLInputPeer peer;
    public int reply_to_msg_id;
    public TLInputMedia media;
    public long random_id;

    public SendMediaL25() {
    }

    public SendMediaL25(int flags, TLInputPeer peer, int reply_to_msg_id, TLInputMedia media, long random_id) {
        this.flags = flags;
        this.peer = peer;
        this.reply_to_msg_id = reply_to_msg_id;
        this.media = media;
        this.random_id = random_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        if ((flags & 1) != 0) {
            reply_to_msg_id = buffer.readInt();
        }
        media = (TLInputMedia) buffer.readTLObject(APIContext.getInstance());
        random_id = buffer.readLong();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(32);
        serializeTo(buffer);
        return buffer;
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeTLObject(peer);
        if ((flags & 1) != 0) {
            buff.writeInt(reply_to_msg_id);
        }
        buff.writeTLObject(media);
        buff.writeLong(random_id);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        int date = (int) (System.currentTimeMillis() / 1000L);
        int msg_id = 0;
        if (context.isAuthorized()) {
            TLMessageMedia messageMedia;
            messageMedia = createMessageMedia();

            if (peer instanceof InputPeerChat) {
                int toChatId = ((InputPeerChat) peer).chat_id;
                int[] users_ids = ChatStore.getInstance().getChatParticipants(toChatId);
                for (int user_id : users_ids) {
                    if (user_id != context.getUserId()) {
                        UpdateNewMessage msg = crateNewChatMessage(context, user_id, toChatId, context.getUserId(), messageMedia);

                        UserModel um = UserStore.getInstance().increment_pts_getUser(context.getUserId(), 0, 1, 0);
                        msg_id = um.sent_messages + um.received_messages + 1;

                        byte[] mediaBytes = ((Message) msg.message).media.serialize().getBytes();
                        DatabaseConnection.getInstance().saveIncomingMessage(user_id, context.getUserId(), toChatId, ((Message) msg.message).id, msg_id,
                                mediaBytes, ((Message) msg.message).flags, ((Message) msg.message).date);

                        DatabaseConnection.getInstance().saveOutgoingMessage(context.getUserId(), user_id, toChatId, msg_id, ((Message) msg.message).id,
                                mediaBytes, 2, ((Message) msg.message).date);

                        Random rnd = new Random();
                        UpdateMessageID umi = new UpdateMessageID(msg_id, rnd.nextLong());
                        TLVector<TLUpdate> updateTLVector = new TLVector<>();
                        updateTLVector.add(umi);
                        msg.pts = um.pts;
                        UpdateNewMessage msg_self = new UpdateNewMessage(new Message(2, msg_id, context.getUserId(),
                                new PeerChat(toChatId), date, "", messageMedia), um.pts, 0);
                        updateTLVector.add(msg_self);
                        TLVector<TLUser> userTLVector = new TLVector<>();
                        userTLVector.add(um.toUser(context.getApiLayer()));
                        TLVector<TLChat> chatsTLVector = new TLVector<>();
                        TLChat c = ChatStore.getInstance().getChat(toChatId);
                        chatsTLVector.add(c);
                        Updates updates = new Updates(updateTLVector, userTLVector, chatsTLVector, date, um.pts);

                        return updates;
                    }
                }
            } else if (peer instanceof InputPeerUser) {
                int toUserId = ((InputPeerUser) peer).user_id;

                UpdateNewMessage msg = crateNewMessage(context, toUserId, context.getUserId(), messageMedia);

                UserModel um = UserStore.getInstance().increment_pts_getUser(context.getUserId(), 0, 1, 0);
                msg_id = um.sent_messages + um.received_messages + 1;

                byte[] mediaBytes = ((Message) msg.message).media.serialize().getBytes();
                DatabaseConnection.getInstance().saveIncomingMessage(toUserId, context.getUserId(), 0, ((Message) msg.message).id, msg_id,
                        mediaBytes, ((Message) msg.message).flags, ((Message) msg.message).date);

                DatabaseConnection.getInstance().saveOutgoingMessage(context.getUserId(), toUserId, 0, msg_id, ((Message) msg.message).id,
                        mediaBytes, 2, ((Message) msg.message).date);

                Random rnd = new Random();
                UpdateMessageID umi = new UpdateMessageID(msg_id, rnd.nextLong());
                TLVector<TLUpdate> updateTLVector = new TLVector<>();
                updateTLVector.add(umi);
                msg.pts = um.pts;
                UpdateNewMessage msg_self = new UpdateNewMessage(new Message(2, msg_id, context.getUserId(),
                        new PeerUser(toUserId), date, "", messageMedia), um.pts, 0);
                updateTLVector.add(msg_self);
                TLVector<TLUser> userTLVector = new TLVector<>();
                userTLVector.add(um.toUser(context.getApiLayer()));
                UserModel uc = UserStore.getInstance().getUser(toUserId);
                userTLVector.add(uc.toUser(context.getApiLayer()));
                Updates updates = new Updates(updateTLVector, userTLVector, new TLVector<TLChat>(), date, um.pts);

                return updates;
            }
        }
        return rpc_error.UNAUTHORIZED();
    }

    private TLMessageMedia createMessageMedia() {
        if (media instanceof InputMediaContact) {
            String phone = stripPhone(((InputMediaContact) media).phone_number);
            int user_id = 0;
            UserModel um = UserStore.getInstance().getUser(phone);
            if (um != null) {
                user_id = um.user_id;
            }
            return new MessageMediaContact(((InputMediaContact) media).phone_number,
                    ((InputMediaContact) media).first_name, ((InputMediaContact) media).last_name,
                    user_id);
        } else if (media instanceof InputMediaPhoto) {//TODO: get photo info from database
            long file_id = ((InputPhoto) ((InputMediaPhoto) media).id).id;
            int file_size = DatabaseConnection.getInstance().getFileSize(file_id);
            TLVector<TLPhotoSize> photoSizes = new TLVector<>();
            Random rnd = new Random();
            photoSizes.add(new PhotoSize("s", new FileLocation(ServerConfig.SERVER_ID, file_id, rnd.nextInt(), file_id), 96, 96, file_size));
            photoSizes.add(new PhotoSize("m", new FileLocation(ServerConfig.SERVER_ID, file_id, rnd.nextInt(), file_id), 256, 256, file_size));
            photoSizes.add(new PhotoSize("x", new FileLocation(ServerConfig.SERVER_ID, file_id, rnd.nextInt(), file_id), 512, 512, file_size));
            return new MessageMediaPhoto(new Photo(file_id,
                    ((InputPhoto) ((InputMediaPhoto) media).id).access_hash, 0, photoSizes),
                    ((InputMediaPhoto) media).caption);
        } else if (media instanceof InputMediaUploadedPhoto) {//TODO: save photo info to database
            long file_id = ((InputFile) ((InputMediaUploadedPhoto) media).file).id;
            int file_size = DatabaseConnection.getInstance().getFileSize(file_id);
            TLVector<TLPhotoSize> photoSizes = new TLVector<>();
            Random rnd = new Random();
            photoSizes.add(new PhotoSize("s", new FileLocation(ServerConfig.SERVER_ID, file_id, rnd.nextInt(), file_id), 96, 96, file_size));
            photoSizes.add(new PhotoSize("m", new FileLocation(ServerConfig.SERVER_ID, file_id, rnd.nextInt(), file_id), 256, 256, file_size));
            photoSizes.add(new PhotoSize("x", new FileLocation(ServerConfig.SERVER_ID, file_id, rnd.nextInt(), file_id), 512, 512, file_size));
            PhotoEmpty pe = new PhotoEmpty(file_id);
            return new MessageMediaPhoto(new Photo(file_id,
                    ((InputFile) ((InputMediaUploadedPhoto) media).file).id, 0, photoSizes),
                    ((InputMediaUploadedPhoto) media).caption);
        } else if (media instanceof InputMediaDocument) {//TODO: get document info from database
            long file_id = ((InputDocument) ((InputMediaDocument) media).document_id).id;
            int file_size = DatabaseConnection.getInstance().getFileSize(file_id);
            return new MessageMediaDocument(new Document(file_id, ((InputDocument) ((InputMediaDocument) media).document_id).access_hash,
                    0, "", file_size, new PhotoSizeEmpty(), ServerConfig.SERVER_ID, new TLVector<TLDocumentAttribute>()));
        } else if (media instanceof InputMediaUploadedDocument) {//TODO: save document info to database
            long file_id = ((InputFile) ((InputMediaUploadedDocument) media).file).id;
            int file_size = DatabaseConnection.getInstance().getFileSize(file_id);
            return new MessageMediaDocument(new Document(file_id, file_id,
                    0, ((InputMediaUploadedDocument) media).mime_type, file_size, new PhotoSizeEmpty(),
                    ServerConfig.SERVER_ID, ((InputMediaUploadedDocument) media).attributes));
        } else if (media instanceof InputMediaUploadedThumbDocument) {//TODO: save document info to database
            long file_id = ((InputFile) ((InputMediaUploadedThumbDocument) media).file).id;
            int file_size = DatabaseConnection.getInstance().getFileSize(file_id);
            return new MessageMediaDocument(new Document(file_id, file_id,
                    0, ((InputMediaUploadedThumbDocument) media).mime_type, file_size, new PhotoSizeEmpty(),
                    ServerConfig.SERVER_ID, ((InputMediaUploadedThumbDocument) media).attributes));
        } else if (media instanceof InputMediaUploadedAudio) {//TODO: save audio info to database
            long file_id = ((InputFile) ((InputMediaUploadedAudio) media).file).id;
            int file_size = DatabaseConnection.getInstance().getFileSize(file_id);
            return new MessageMediaAudio(new Audio(file_id, ((InputAudio) ((InputMediaAudio) media).audio_id).access_hash,
                    0, ((InputMediaUploadedAudio) media).duration, ((InputMediaUploadedAudio) media).mime_type,
                    file_size, ServerConfig.SERVER_ID));
        } else if (media instanceof InputMediaAudio) {//TODO: get audio info from database
            long file_id = ((InputAudio) ((InputMediaAudio) media).audio_id).id;
            int file_size = DatabaseConnection.getInstance().getFileSize(file_id);
            return new MessageMediaAudio(new Audio(file_id, ((InputAudio) ((InputMediaAudio) media).audio_id).access_hash,
                    0, 0, "", file_size, ServerConfig.SERVER_ID));
        } else if (media instanceof InputMediaUploadedVideo) {//TODO: save video info to database
            long file_id = ((InputFile) ((InputMediaUploadedVideo) media).file).id;
            int file_size = DatabaseConnection.getInstance().getFileSize(file_id);
            return new MessageMediaVideo(new Video(file_id, ((InputVideo) ((InputMediaVideo) media).video_id).access_hash,
                    0, ((InputMediaUploadedVideo) media).duration, ((InputMediaUploadedVideo) media).caption,
                    file_size, new PhotoSizeEmpty(), ServerConfig.SERVER_ID, ((InputMediaUploadedVideo) media).w,
                    ((InputMediaUploadedVideo) media).h), ((InputMediaVideo) media).caption);
        } else if (media instanceof InputMediaVideo) {//TODO: get video info from database
            long file_id = ((InputVideo) ((InputMediaVideo) media).video_id).id;
            int file_size = DatabaseConnection.getInstance().getFileSize(file_id);
            return new MessageMediaVideo(new Video(file_id, ((InputVideo) ((InputMediaVideo) media).video_id).access_hash,
                    0, 0, "", file_size, new PhotoSizeEmpty(), ServerConfig.SERVER_ID, 0, 0), ((InputMediaVideo) media).caption);
        } else if (media instanceof InputMediaGeoPoint) {
            return new MessageMediaGeo(new GeoPoint(((InputGeoPoint) ((InputMediaGeoPoint) media).geo_point).lon,
                    ((InputGeoPoint) ((InputMediaGeoPoint) media).geo_point).lat));
        } else if (media instanceof InputMediaVenue) {
            GeoPoint geo = new GeoPoint(((InputGeoPoint) ((InputMediaVenue) media).geo_point).lon,
                    ((InputGeoPoint) ((InputMediaGeoPoint) media).geo_point).lat);
            return new MessageMediaVenue(geo, ((InputMediaVenue) media).title, ((InputMediaVenue) media).address,
                    ((InputMediaVenue) media).provider, ((InputMediaVenue) media).venue_id);
        } else {
            return new MessageMediaEmpty();
        }
    }

    private String stripPhone(String phone) {
        StringBuilder res = new StringBuilder(phone);
        String phoneChars = "0123456789";
        for (int i = res.length() - 1; i >= 0; i--) {
            if (!phoneChars.contains(res.substring(i, i + 1))) {
                res.deleteCharAt(i);
            }
        }
        return res.toString();
    }

    public UpdateNewMessage crateNewMessage(TLContext context, int to_user_id, int from_user_id, TLMessageMedia media) {
        int date = (int) (System.currentTimeMillis() / 1000L);
        int msg_id;

        UserModel um = UserStore.getInstance().increment_pts_getUser(to_user_id, 1, 0, 1);
        msg_id = um.sent_messages + um.received_messages + 1;

        int flags_msg = 1;
        UpdateNewMessage msg = new UpdateNewMessage(new Message(flags_msg, msg_id, from_user_id,
                new PeerUser(to_user_id), date, "", media), um.pts, 1);

        TLVector<TLUpdate> updateTLVector = new TLVector<>();
        updateTLVector.add(msg);

        UserModel uc = UserStore.getInstance().getUser(from_user_id);

        TLVector<TLUser> userTLVector = new TLVector<>();
        userTLVector.add(um.toUser(context.getApiLayer()));
        userTLVector.add(uc.toUser(context.getApiLayer()));

        UpdatesCombined updatesCombined = new UpdatesCombined(updateTLVector, userTLVector, new TLVector<TLChat>(), date, um.pts, um.pts);

        Router.getInstance().Route(to_user_id, updatesCombined, false);

        return msg;
    }

    public UpdateNewMessage crateNewChatMessage(TLContext context, int to_user_id, int to_chat_id, int from_user_id, TLMessageMedia media) {
        int date = (int) (System.currentTimeMillis() / 1000L);
        int msg_id;

        UserModel um = UserStore.getInstance().increment_pts_getUser(to_user_id, 1, 0, 1);
        msg_id = um.sent_messages + um.received_messages + 1;

        int flags_msg = 1;
        UpdateNewMessage msg = new UpdateNewMessage(new Message(flags_msg, msg_id, from_user_id,
                new PeerChat(to_chat_id), date, "", media), um.pts, 1);

        TLVector<TLUpdate> updateTLVector = new TLVector<>();
        updateTLVector.add(msg);

        UserModel uc = UserStore.getInstance().getUser(from_user_id);

        TLVector<TLUser> userTLVector = new TLVector<>();
        userTLVector.add(um.toUser(context.getApiLayer()));
        userTLVector.add(uc.toUser(context.getApiLayer()));

        TLVector<TLChat> chatsTLVector = new TLVector<>();
        TLChat c = ChatStore.getInstance().getChat(to_chat_id);
        chatsTLVector.add(c);

        UpdatesCombined updatesCombined = new UpdatesCombined(updateTLVector, userTLVector, chatsTLVector, date, um.pts, um.pts);

        Router.getInstance().Route(to_user_id, updatesCombined, false);

        return msg;
    }
}