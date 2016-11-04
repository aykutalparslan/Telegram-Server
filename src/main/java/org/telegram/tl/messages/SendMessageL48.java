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
import org.telegram.tl.*;
import org.telegram.tl.service.rpc_error;

public class SendMessageL48 extends TLObject implements TLMethod {

    public static final int ID = 0xfa88427a;

    public int flags;
    public TLInputPeer peer;
    public int reply_to_msg_id;
    public String message;
    public long random_id;
    public TLReplyMarkup reply_markup;
    public TLVector<TLMessageEntity> entities;

    public SendMessageL48() {
        this.entities = new TLVector<>();
    }

    public SendMessageL48(int flags, TLInputPeer peer, int reply_to_msg_id, String message, long random_id, TLReplyMarkup reply_markup, TLVector<TLMessageEntity> entities) {
        this.flags = flags;
        this.peer = peer;
        this.reply_to_msg_id = reply_to_msg_id;
        this.message = message;
        this.random_id = random_id;
        this.reply_markup = reply_markup;
        this.entities = entities;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        if ((flags & (1 << 0)) != 0) {
            reply_to_msg_id = buffer.readInt();
        }
        message = buffer.readString();
        random_id = buffer.readLong();
        if ((flags & (1 << 2)) != 0) {
            reply_markup = (TLReplyMarkup) buffer.readTLObject(APIContext.getInstance());
        }
        if ((flags & (1 << 3)) != 0) {
            entities = (TLVector<TLMessageEntity>) buffer.readTLVector(TLMessageEntity.class);
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(32);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (reply_to_msg_id != 0) {
            flags |= (1 << 0);
        }
        if (reply_markup != null) {
            flags |= (1 << 2);
        }
        if (entities != null) {
            flags |= (1 << 3);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeTLObject(peer);
        if ((flags & (1 << 0)) != 0) {
            buff.writeInt(reply_to_msg_id);
        }
        buff.writeString(message);
        buff.writeLong(random_id);
        if ((flags & (1 << 2)) != 0) {
            buff.writeTLObject(reply_markup);
        }
        if ((flags & (1 << 3)) != 0) {
            buff.writeTLObject(entities);
        }
    }

    public boolean is_no_webpage() {
        return (flags & (1 << 1)) != 0;
    }

    public boolean set_no_webpage() {
        return (flags |= (1 << 1)) != 0;
    }

    public boolean is_silent() {
        return (flags & (1 << 5)) != 0;
    }

    public boolean set_silent() {
        return (flags |= (1 << 5)) != 0;
    }

    public boolean is_background() {
        return (flags & (1 << 6)) != 0;
    }

    public boolean set_background() {
        return (flags |= (1 << 6)) != 0;
    }

    public boolean is_clear_draft() {
        return (flags & (1 << 7)) != 0;
    }

    public boolean set_clear_draft() {
        return (flags |= (1 << 7)) != 0;
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        int date = (int) (System.currentTimeMillis() / 1000L);
        int msg_id = 0;
        int pts = 0;
        if (context.isAuthorized()) {
            if (peer instanceof InputPeerUser) {
                int toUserId = ((InputPeerUser) peer).user_id;

                UserModel umc = UserStore.getInstance().increment_pts_getUser(toUserId, 1, 0, 1);

                msg_id = umc.sent_messages + umc.received_messages + 1;

                UpdateShortMessageL48 msg_48 = crateShortMessageL48(msg_id, umc.pts, context.getUserId(), this.message, this.entities);
                UpdateShortMessage msg = crateShortMessage(msg_id, umc.pts, context.getUserId(), this.message, this.entities);

                DatabaseConnection.getInstance().saveIncomingMessage(toUserId, context.getUserId(), 0, msg_48.id, msg_id,
                        msg_48.message, msg_48.flags, msg_48.date);



                Object[] sessions = Router.getInstance().getActiveSessions(toUserId);

                for (Object session : sessions) {
                    if (((ActiveSession) session).layer >= 48) {
                        Router.getInstance().Route(((ActiveSession) session).session_id, ((ActiveSession) session).auth_key_id, msg_48, false);
                    } else {
                        Router.getInstance().Route(((ActiveSession) session).session_id, ((ActiveSession) session).auth_key_id, msg, false);
                    }
                }
                UserModel um;
                if (context.getApiLayer() >= 57) {
                    um = UserStore.getInstance().increment_pts_getUser(context.getUserId(), 0, 1, 0);
                    msg_id = um.sent_messages + um.received_messages + 1;
                    DatabaseConnection.getInstance().saveOutgoingMessage(context.getUserId(), toUserId, 0, msg_id, msg_48.id,
                            msg_48.message, 2, msg_48.date);

                    UpdateShortSentMessage updateShortSentMessage = new UpdateShortSentMessage(0, msg_id, um.pts, 0, date,
                            new MessageMediaEmpty(), entities);
                    updateShortSentMessage.set_updateShortSentMessage_out();
                    return updateShortSentMessage;
                } else if (context.getApiLayer() >= 48) {
                    um = UserStore.getInstance().increment_pts_getUser(context.getUserId(), 1, 1, 0);
                    msg_id = um.sent_messages + um.received_messages + 1;
                    DatabaseConnection.getInstance().saveOutgoingMessage(context.getUserId(), toUserId, 0, msg_id, msg_48.id,
                            msg_48.message, 2, msg_48.date);

                    UpdateShortSentMessage updateShortSentMessage = new UpdateShortSentMessage(0, msg_id, um.pts, 1, date,
                            new MessageMediaEmpty(), entities);
                    updateShortSentMessage.set_updateShortSentMessage_out();
                    return updateShortSentMessage;
                } else {
                    um = UserStore.getInstance().increment_pts_getUser(context.getUserId(), 0, 1, 0);
                    msg_id = um.sent_messages + um.received_messages + 1;
                    DatabaseConnection.getInstance().saveOutgoingMessage(context.getUserId(), toUserId, 0, msg_id, msg_48.id,
                            msg_48.message, 2, msg_48.date);

                    return new SentMessage(msg_id, date, new MessageMediaEmpty(),
                            new TLVector<TLMessageEntity>(), pts, 0, pts);
                }
            } else if (peer instanceof InputPeerChat) {
                int toChatId = ((InputPeerChat) peer).chat_id;
                int[] users_ids = ChatStore.getInstance().getChatParticipants(toChatId);
                for (int user_id : users_ids) {
                    if (user_id != context.getUserId()) {

                        int msg_id_peer;

                        UserModel umc = UserStore.getInstance().increment_pts_getUser(user_id, 1, 0, 1);
                        msg_id_peer = umc.sent_messages + umc.received_messages + 1;

                        UpdateShortChatMessage msg = crateShortChatMessage(msg_id_peer, umc.pts, toChatId,
                                context.getUserId(), this.message, this.entities);

                        UpdateShortChatMessageL48 msg_48 = crateShortChatMessageL48(msg_id_peer, umc.pts, toChatId,
                                context.getUserId(), this.message, this.entities);

                        DatabaseConnection.getInstance().saveIncomingMessage(user_id, context.getUserId(), toChatId, msg.id, msg_id,
                                msg.message, msg.flags, msg.date);

                        Object[] sessions = Router.getInstance().getActiveSessions(user_id);

                        for (Object session : sessions) {
                            if (((ActiveSession) session).layer >= 48) {
                                Router.getInstance().Route(((ActiveSession) session).session_id, ((ActiveSession) session).auth_key_id, msg_48, false);
                            } else {
                                Router.getInstance().Route(((ActiveSession) session).session_id, ((ActiveSession) session).auth_key_id, msg, false);
                            }
                        }
                    }
                }

                UserModel um = UserStore.getInstance().increment_pts_getUser(context.getUserId(), 0, 1, 0);
                msg_id = um.sent_messages + um.received_messages + 1;

                DatabaseConnection.getInstance().saveOutgoingMessage(context.getUserId(), 0, toChatId, msg_id, 0,
                        this.message, 2, date);

                pts = um.pts;

                if (context.getApiLayer() >= 48) {
                    UpdateShortSentMessage updateShortSentMessage = new UpdateShortSentMessage(0, msg_id, pts, 1, date,
                            new MessageMediaEmpty(), entities);
                    updateShortSentMessage.set_updateShortSentMessage_out();
                    return updateShortSentMessage;
                } else {
                    return new SentMessage(msg_id, date, new MessageMediaEmpty(),
                            new TLVector<TLMessageEntity>(), pts, 0, pts);
                }
            }
        }

        return rpc_error.UNAUTHORIZED();
    }

    public UpdateShortMessage crateShortMessage(int message_id, int pts, int from_user_id, String message, TLVector<TLMessageEntity> entities) {
        int date = (int) (System.currentTimeMillis() / 1000L);

        int flags_msg = 1;
        UpdateShortMessage msg = new UpdateShortMessage(flags_msg, message_id,
                from_user_id, message, pts, 1,
                date, 0, 0, 0, entities);

        return msg;
    }

    public UpdateShortMessageL48 crateShortMessageL48(int message_id, int pts, int from_user_id, String message, TLVector<TLMessageEntity> entities) {
        int date = (int) (System.currentTimeMillis() / 1000L);

        int flags_msg = 1;
        UpdateShortMessageL48 msg = new UpdateShortMessageL48(flags_msg, message_id, from_user_id, message,
                pts, 1, date, null, 0, 0, entities);

        return msg;
    }

    public UpdateShortChatMessage crateShortChatMessage(int message_id, int pts, int to_chat_id, int from_user_id, String message, TLVector<TLMessageEntity> entities) {
        int date = (int) (System.currentTimeMillis() / 1000L);

        int flags_msg = 1;
        UpdateShortChatMessage msg = new UpdateShortChatMessage(flags_msg, message_id,
                from_user_id, to_chat_id, message, pts, 1,
                date, 0, 0, 0, entities);

        return msg;
    }

    public UpdateShortChatMessageL48 crateShortChatMessageL48(int message_id, int pts, int to_chat_id, int from_user_id, String message, TLVector<TLMessageEntity> entities) {
        int date = (int) (System.currentTimeMillis() / 1000L);

        int flags_msg = 1;
        UpdateShortChatMessageL48 msg = new UpdateShortChatMessageL48(flags_msg, message_id,
                from_user_id, to_chat_id, message, pts, 1,
                date, null, 0, 0, entities);

        return msg;
    }
}