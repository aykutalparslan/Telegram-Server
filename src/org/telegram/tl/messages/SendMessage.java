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

import org.telegram.core.TLContext;
import org.telegram.core.TLMethod;
import org.telegram.core.UpdatesQueue;
import org.telegram.core.UserStore;
import org.telegram.data.DatabaseConnection;
import org.telegram.data.UserModel;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

import java.util.ArrayList;

public class SendMessage extends TLObject implements TLMethod {

    public static final int ID = 0xdf12390;

    public int flags;
    public TLInputPeer peer;
    public int reply_to_msg_id;
    public String message;
    public long random_id;
    public TLReplyMarkup reply_markup;
    public TLVector<TLMessageEntity> entities = new TLVector<>();

    public SendMessage() {
    }

    public SendMessage(int flags, TLInputPeer peer, int reply_to_msg_id, String message, long random_id,
                       TLReplyMarkup reply_markup, TLVector<TLMessageEntity> entities) {
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
        if ((flags & 1) != 0) {
            reply_to_msg_id = buffer.readInt();
        }
        message = buffer.readString();
        random_id = buffer.readLong();
        if ((flags & 4) != 0) {
            reply_markup = (TLReplyMarkup) buffer.readTLObject(APIContext.getInstance());
        }
        if ((flags & 8) != 0) {
            entities = (TLVector<TLMessageEntity>) buffer.readTLObject(APIContext.getInstance());
        }
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
        buff.writeString(message);
        buff.writeLong(random_id);
        if ((flags & 4) != 0) {
            buff.writeTLObject(reply_markup);
        }
        if ((flags & 8) != 0) {
            buff.writeTLObject(entities);
        }
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
            int toUserId = ((InputPeerUser) peer).user_id;
            UpdateShortMessage msg = (UpdateShortMessage) UpdatesQueue.getInstance().sendMessage(toUserId,
                    context.getUserId(), this.message, this.entities);

            UserModel um = UserStore.getInstance().increment_pts_getUser(context.getUserId(), 0, 1, 0);
            msg_id = um.sent_messages + um.received_messages + 1;

            DatabaseConnection.getInstance().saveIncomingMessage(toUserId, context.getUserId(), msg.id, msg_id,
                    msg.message, msg.flags, msg.date);

            DatabaseConnection.getInstance().saveOutgoingMessage(context.getUserId(), toUserId, msg_id, msg.id,
                    msg.message, 2, msg.date);

            pts = um.pts;
        }

        return new SentMessage(msg_id, date, new MessageMediaEmpty(), new TLVector<TLMessageEntity>(), pts, 0, pts);
    }
}