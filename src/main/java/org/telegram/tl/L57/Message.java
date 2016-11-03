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

package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class Message extends org.telegram.tl.TLMessage {

    public static final int ID = 0xc09be45f;

    public int flags;
    public int id;
    public int from_id;
    public org.telegram.tl.TLPeer to_id;
    public org.telegram.tl.TLMessageFwdHeader fwd_from;
    public int via_bot_id;
    public int reply_to_msg_id;
    public int date;
    public String message;
    public org.telegram.tl.TLMessageMedia media;
    public org.telegram.tl.TLReplyMarkup reply_markup;
    public TLVector<org.telegram.tl.TLMessageEntity> entities;
    public int views;
    public int edit_date;

    public Message() {
        this.entities = new TLVector<>();
    }

    public Message(int flags, int id, int from_id, org.telegram.tl.TLPeer to_id, org.telegram.tl.TLMessageFwdHeader fwd_from, int via_bot_id, int reply_to_msg_id, int date, String message, org.telegram.tl.TLMessageMedia media, org.telegram.tl.TLReplyMarkup reply_markup, TLVector<org.telegram.tl.TLMessageEntity> entities, int views, int edit_date) {
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

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = buffer.readInt();
        if ((flags & (1 << 8)) != 0) {
            from_id = buffer.readInt();
        }
        to_id = (org.telegram.tl.TLPeer) buffer.readTLObject(APIContext.getInstance());
        if ((flags & (1 << 2)) != 0) {
            fwd_from = (org.telegram.tl.TLMessageFwdHeader) buffer.readTLObject(APIContext.getInstance());
        }
        if ((flags & (1 << 11)) != 0) {
            via_bot_id = buffer.readInt();
        }
        if ((flags & (1 << 3)) != 0) {
            reply_to_msg_id = buffer.readInt();
        }
        date = buffer.readInt();
        message = buffer.readString();
        if ((flags & (1 << 9)) != 0) {
            media = (org.telegram.tl.TLMessageMedia) buffer.readTLObject(APIContext.getInstance());
        }
        if ((flags & (1 << 6)) != 0) {
            reply_markup = (org.telegram.tl.TLReplyMarkup) buffer.readTLObject(APIContext.getInstance());
        }
        if ((flags & (1 << 7)) != 0) {
            entities = (TLVector<org.telegram.tl.TLMessageEntity>) buffer.readTLVector(org.telegram.tl.TLMessageEntity.class);
        }
        if ((flags & (1 << 10)) != 0) {
            views = buffer.readInt();
        }
        if ((flags & (1 << 15)) != 0) {
            edit_date = buffer.readInt();
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(144);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (from_id != 0) {
            flags |= (1 << 8);
        }
        if (fwd_from != null) {
            flags |= (1 << 2);
        }
        if (via_bot_id != 0) {
            flags |= (1 << 11);
        }
        if (reply_to_msg_id != 0) {
            flags |= (1 << 3);
        }
        if (media != null) {
            flags |= (1 << 9);
        }
        if (reply_markup != null) {
            flags |= (1 << 6);
        }
        if (entities != null) {
            flags |= (1 << 7);
        }
        if (views != 0) {
            flags |= (1 << 10);
        }
        if (edit_date != 0) {
            flags |= (1 << 15);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeInt(id);
        if ((flags & (1 << 8)) != 0) {
            buff.writeInt(from_id);
        }
        buff.writeTLObject(to_id);
        if ((flags & (1 << 2)) != 0) {
            buff.writeTLObject(fwd_from);
        }
        if ((flags & (1 << 11)) != 0) {
            buff.writeInt(via_bot_id);
        }
        if ((flags & (1 << 3)) != 0) {
            buff.writeInt(reply_to_msg_id);
        }
        buff.writeInt(date);
        buff.writeString(message);
        if ((flags & (1 << 9)) != 0) {
            buff.writeTLObject(media);
        }
        if ((flags & (1 << 6)) != 0) {
            buff.writeTLObject(reply_markup);
        }
        if ((flags & (1 << 7)) != 0) {
            buff.writeTLObject(entities);
        }
        if ((flags & (1 << 10)) != 0) {
            buff.writeInt(views);
        }
        if ((flags & (1 << 15)) != 0) {
            buff.writeInt(edit_date);
        }
    }

    public boolean is_out() {
        return (flags & (1 << 1)) != 0;
    }

    public void set_out(boolean v) {
        if (v) {
            flags |= (1 << 1);
        } else {
            flags &= ~(1 << 1);
        }
    }

    public boolean is_mentioned() {
        return (flags & (1 << 4)) != 0;
    }

    public void set_mentioned(boolean v) {
        if (v) {
            flags |= (1 << 4);
        } else {
            flags &= ~(1 << 4);
        }
    }

    public boolean is_media_unread() {
        return (flags & (1 << 5)) != 0;
    }

    public void set_media_unread(boolean v) {
        if (v) {
            flags |= (1 << 5);
        } else {
            flags &= ~(1 << 5);
        }
    }

    public boolean is_silent() {
        return (flags & (1 << 13)) != 0;
    }

    public void set_silent(boolean v) {
        if (v) {
            flags |= (1 << 13);
        } else {
            flags &= ~(1 << 13);
        }
    }

    public boolean is_post() {
        return (flags & (1 << 14)) != 0;
    }

    public void set_post(boolean v) {
        if (v) {
            flags |= (1 << 14);
        } else {
            flags &= ~(1 << 14);
        }
    }

    public int getConstructor() {
        return ID;
    }
}