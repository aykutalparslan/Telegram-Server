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

public class DocumentAttributeAudio extends org.telegram.tl.TLDocumentAttribute {

    public static final int ID = 0x9852f9c6;

    public int flags;
    public int duration;
    public String title;
    public String performer;
    public byte[] waveform;

    public DocumentAttributeAudio() {
    }

    public DocumentAttributeAudio(int flags, int duration, String title, String performer, byte[] waveform) {
        this.flags = flags;
        this.duration = duration;
        this.title = title;
        this.performer = performer;
        this.waveform = waveform;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        duration = buffer.readInt();
        if ((flags & (1 << 0)) != 0) {
            title = buffer.readString();
        }
        if ((flags & (1 << 1)) != 0) {
            performer = buffer.readString();
        }
        if ((flags & (1 << 2)) != 0) {
            waveform = buffer.readBytes();
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(44);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (title != null && !title.isEmpty()) {
            flags |= (1 << 0);
        }
        if (performer != null && !performer.isEmpty()) {
            flags |= (1 << 1);
        }
        if (waveform.length != 0) {
            flags |= (1 << 2);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeInt(duration);
        if ((flags & (1 << 0)) != 0) {
            buff.writeString(title);
        }
        if ((flags & (1 << 1)) != 0) {
            buff.writeString(performer);
        }
        if ((flags & (1 << 2)) != 0) {
            buff.writeBytes(waveform);
        }
    }

    public boolean is_voice() {
        return (flags & (1 << 10)) != 0;
    }

    public void set_voice(boolean v) {
        if (v) {
            flags |= (1 << 10);
        } else {
            flags &= ~(1 << 10);
        }
    }

    public int getConstructor() {
        return ID;
    }
}