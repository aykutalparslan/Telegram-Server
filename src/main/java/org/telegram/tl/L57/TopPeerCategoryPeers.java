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

public class TopPeerCategoryPeers extends org.telegram.tl.TLTopPeerCategoryPeers {

    public static final int ID = 0xfb834291;

    public org.telegram.tl.TLTopPeerCategory category;
    public int count;
    public TLVector<org.telegram.tl.TLTopPeer> peers;

    public TopPeerCategoryPeers() {
        this.peers = new TLVector<>();
    }

    public TopPeerCategoryPeers(org.telegram.tl.TLTopPeerCategory category, int count, TLVector<org.telegram.tl.TLTopPeer> peers) {
        this.category = category;
        this.count = count;
        this.peers = peers;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        category = (org.telegram.tl.TLTopPeerCategory) buffer.readTLObject(APIContext.getInstance());
        count = buffer.readInt();
        peers = (TLVector<org.telegram.tl.TLTopPeer>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(24);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeTLObject(category);
        buff.writeInt(count);
        buff.writeTLObject(peers);
    }


    public int getConstructor() {
        return ID;
    }
}