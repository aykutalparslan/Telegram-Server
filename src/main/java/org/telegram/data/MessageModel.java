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

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

import java.io.Serializable;

public class MessageModel implements Serializable {

    public int flags;
    public int id;
    public int from_id;
    public TLPeer to_id;
    public TLMessageFwdHeader fwd_from;
    public int via_bot_id;
    public int reply_to_msg_id;
    public int date;
    public String message;
    public TLMessageMedia media;
    public TLReplyMarkup reply_markup;
    public TLVector<TLMessageEntity> entities;
    public int views;
    public int edit_date;

    public MessageModel(int flags, int id, int from_id, TLPeer to_id, TLMessageFwdHeader fwd_from, int via_bot_id,
                        int reply_to_msg_id, int date, String message, TLMessageMedia media,
                        TLReplyMarkup reply_markup, TLVector<TLMessageEntity> entities, int views, int edit_date) {
        this.flags = flags;
        this.id = id;
        this.from_id = from_id;
        this.to_id = to_id;
        this.fwd_from = fwd_from;
        this.via_bot_id = via_bot_id;
        this.reply_to_msg_id = reply_to_msg_id;
        this.date = date;
        this.message = message;
        this.media = media;
        this.reply_markup = reply_markup;
        this.entities = entities;
        this.views = views;
        this.edit_date = edit_date;
    }

    public Message toMessage() {
        return new Message(flags, id, from_id, to_id, date, message, media);
    }

    public MessageL48 toMessageL48() {
        return new MessageL48(flags, id, from_id, to_id, fwd_from, via_bot_id,
                reply_to_msg_id, date, message, media, reply_markup, entities, views, edit_date);
    }
}