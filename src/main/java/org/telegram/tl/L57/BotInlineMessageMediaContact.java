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

public class BotInlineMessageMediaContact extends org.telegram.tl.TLBotInlineMessage {

    public static final int ID = 0x35edb4d4;

    public int flags;
    public String phone_number;
    public String first_name;
    public String last_name;
    public org.telegram.tl.TLReplyMarkup reply_markup;

    public BotInlineMessageMediaContact() {
    }

    public BotInlineMessageMediaContact(int flags, String phone_number, String first_name, String last_name, org.telegram.tl.TLReplyMarkup reply_markup) {
        this.flags = flags;
        this.phone_number = phone_number;
        this.first_name = first_name;
        this.last_name = last_name;
        this.reply_markup = reply_markup;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        phone_number = buffer.readString();
        first_name = buffer.readString();
        last_name = buffer.readString();
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
        if (reply_markup != null) {
            flags |= (1 << 2);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeString(phone_number);
        buff.writeString(first_name);
        buff.writeString(last_name);
        if ((flags & (1 << 2)) != 0) {
            buff.writeTLObject(reply_markup);
        }
    }


    public int getConstructor() {
        return ID;
    }
}