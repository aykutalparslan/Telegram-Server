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

public class BotInlineMessageText extends org.telegram.tl.TLBotInlineMessage {

    public static final int ID = 0x8c7f65e2;

    public int flags;
    public String message;
    public TLVector<org.telegram.tl.TLMessageEntity> entities;
    public org.telegram.tl.TLReplyMarkup reply_markup;

    public BotInlineMessageText() {
        this.entities = new TLVector<>();
    }

    public BotInlineMessageText(int flags, String message, TLVector<org.telegram.tl.TLMessageEntity> entities, org.telegram.tl.TLReplyMarkup reply_markup) {
        this.flags = flags;
        this.message = message;
        this.entities = entities;
        this.reply_markup = reply_markup;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        message = buffer.readString();
        if ((flags & (1 << 1)) != 0) {
            entities = (TLVector<org.telegram.tl.TLMessageEntity>) buffer.readTLVector(org.telegram.tl.TLMessageEntity.class);
        }
        if ((flags & (1 << 2)) != 0) {
            reply_markup = (org.telegram.tl.TLReplyMarkup) buffer.readTLObject(APIContext.getInstance());
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(40);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (entities != null) {
            flags |= (1 << 1);
        }
        if (reply_markup != null) {
            flags |= (1 << 2);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeString(message);
        if ((flags & (1 << 1)) != 0) {
            buff.writeTLObject(entities);
        }
        if ((flags & (1 << 2)) != 0) {
            buff.writeTLObject(reply_markup);
        }
    }

    public boolean is_no_webpage() {
        return (flags & (1 << 0)) != 0;
    }

    public void set_no_webpage(boolean v) {
        if (v) {
            flags |= (1 << 0);
        } else {
            flags &= ~(1 << 0);
        }
    }

    public int getConstructor() {
        return ID;
    }
}