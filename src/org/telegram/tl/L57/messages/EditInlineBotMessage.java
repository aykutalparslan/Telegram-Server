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

package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class EditInlineBotMessage extends TLObject {

    public static final int ID = 0x130c2c85;

    public int flags;
    public org.telegram.tl.TLInputBotInlineMessageID id;
    public String message;
    public org.telegram.tl.TLReplyMarkup reply_markup;
    public TLVector<org.telegram.tl.TLMessageEntity> entities;

    public EditInlineBotMessage() {
        this.entities = new TLVector<>();
    }

    public EditInlineBotMessage(int flags, org.telegram.tl.TLInputBotInlineMessageID id, String message, org.telegram.tl.TLReplyMarkup reply_markup, TLVector<org.telegram.tl.TLMessageEntity> entities) {
        this.flags = flags;
        this.id = id;
        this.message = message;
        this.reply_markup = reply_markup;
        this.entities = entities;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = (org.telegram.tl.TLInputBotInlineMessageID) buffer.readTLObject(APIContext.getInstance());
        if ((flags & (1 << 11)) != 0) {
            message = buffer.readString();
        }
        if ((flags & (1 << 2)) != 0) {
            reply_markup = (org.telegram.tl.TLReplyMarkup) buffer.readTLObject(APIContext.getInstance());
        }
        if ((flags & (1 << 3)) != 0) {
            entities = (TLVector<org.telegram.tl.TLMessageEntity>) buffer.readTLVector(org.telegram.tl.TLMessageEntity.class);
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(48);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (message != null && !message.isEmpty()) {
            flags |= (1 << 11);
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
        buff.writeTLObject(id);
        if ((flags & (1 << 11)) != 0) {
            buff.writeString(message);
        }
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

    public void set_no_webpage(boolean v) {
        if (v) {
            flags |= (1 << 1);
        } else {
            flags &= ~(1 << 1);
        }
    }

    public int getConstructor() {
        return ID;
    }
}