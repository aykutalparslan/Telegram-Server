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

public class BotCallbackAnswer extends org.telegram.tl.messages.TLBotCallbackAnswer {

    public static final int ID = 0xb10df1fb;

    public int flags;
    public String message;
    public String url;

    public BotCallbackAnswer() {
    }

    public BotCallbackAnswer(int flags, String message, String url) {
        this.flags = flags;
        this.message = message;
        this.url = url;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        if ((flags & (1 << 0)) != 0) {
            message = buffer.readString();
        }
        if ((flags & (1 << 2)) != 0) {
            url = buffer.readString();
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
        if (message != null && !message.isEmpty()) {
            flags |= (1 << 0);
        }
        if (url != null && !url.isEmpty()) {
            flags |= (1 << 2);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        if ((flags & (1 << 0)) != 0) {
            buff.writeString(message);
        }
        if ((flags & (1 << 2)) != 0) {
            buff.writeString(url);
        }
    }

    public boolean is_alert() {
        return (flags & (1 << 1)) != 0;
    }

    public void set_alert(boolean v) {
        if (v) {
            flags |= (1 << 1);
        } else {
            flags &= ~(1 << 1);
        }
    }

    public boolean is_has_url() {
        return (flags & (1 << 3)) != 0;
    }

    public void set_has_url(boolean v) {
        if (v) {
            flags |= (1 << 3);
        } else {
            flags &= ~(1 << 3);
        }
    }

    public int getConstructor() {
        return ID;
    }
}