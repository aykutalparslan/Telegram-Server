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

public class Dialog extends org.telegram.tl.TLDialog {

    public static final int ID = 0x66ffba14;

    public int flags;
    public org.telegram.tl.TLPeer peer;
    public int top_message;
    public int read_inbox_max_id;
    public int read_outbox_max_id;
    public int unread_count;
    public org.telegram.tl.TLPeerNotifySettings notify_settings;
    public int pts;
    public org.telegram.tl.TLDraftMessage draft;

    public Dialog() {
    }

    public Dialog(int flags, org.telegram.tl.TLPeer peer, int top_message, int read_inbox_max_id, int read_outbox_max_id, int unread_count, org.telegram.tl.TLPeerNotifySettings notify_settings, int pts, org.telegram.tl.TLDraftMessage draft) {
        this.flags = flags;
        this.peer = peer;
        this.top_message = top_message;
        this.read_inbox_max_id = read_inbox_max_id;
        this.read_outbox_max_id = read_outbox_max_id;
        this.unread_count = unread_count;
        this.notify_settings = notify_settings;
        this.pts = pts;
        this.draft = draft;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        peer = (org.telegram.tl.TLPeer) buffer.readTLObject(APIContext.getInstance());
        top_message = buffer.readInt();
        read_inbox_max_id = buffer.readInt();
        read_outbox_max_id = buffer.readInt();
        unread_count = buffer.readInt();
        notify_settings = (org.telegram.tl.TLPeerNotifySettings) buffer.readTLObject(APIContext.getInstance());
        if ((flags & (1 << 0)) != 0) {
            pts = buffer.readInt();
        }
        if ((flags & (1 << 1)) != 0) {
            draft = (org.telegram.tl.TLDraftMessage) buffer.readTLObject(APIContext.getInstance());
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(56);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (pts != 0) {
            flags |= (1 << 0);
        }
        if (draft != null) {
            flags |= (1 << 1);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeTLObject(peer);
        buff.writeInt(top_message);
        buff.writeInt(read_inbox_max_id);
        buff.writeInt(read_outbox_max_id);
        buff.writeInt(unread_count);
        buff.writeTLObject(notify_settings);
        if ((flags & (1 << 0)) != 0) {
            buff.writeInt(pts);
        }
        if ((flags & (1 << 1)) != 0) {
            buff.writeTLObject(draft);
        }
    }


    public int getConstructor() {
        return ID;
    }
}