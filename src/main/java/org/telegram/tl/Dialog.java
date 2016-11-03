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

package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class Dialog extends TLDialog {

    public static final int ID = 0xc1dd804a;

    public TLPeer peer;
    public int top_message;
    public int read_inbox_max_id;
    public int unread_count;
    public TLPeerNotifySettings notify_settings;

    public Dialog() {
    }

    public Dialog(TLPeer peer, int top_message, int read_inbox_max_id, int unread_count, TLPeerNotifySettings notify_settings) {
        this.peer = peer;
        this.top_message = top_message;
        this.read_inbox_max_id = read_inbox_max_id;
        this.unread_count = unread_count;
        this.notify_settings = notify_settings;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLPeer) buffer.readTLObject(APIContext.getInstance());
        top_message = buffer.readInt();
        read_inbox_max_id = buffer.readInt();
        unread_count = buffer.readInt();
        notify_settings = (TLPeerNotifySettings) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(peer);
        buff.writeInt(top_message);
        buff.writeInt(read_inbox_max_id);
        buff.writeInt(unread_count);
        buff.writeTLObject(notify_settings);
    }

    public int getConstructor() {
        return ID;
    }
}