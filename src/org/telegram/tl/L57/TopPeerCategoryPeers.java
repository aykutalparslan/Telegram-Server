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

public class TopPeerCategoryPeers extends TLTopPeerCategoryPeers {

    public static final int ID = 0xfb834291;

    public TLTopPeerCategory category;
    public int count;
    public TLVector<TLTopPeer> peers;

    public TopPeerCategoryPeers() {
        this.peers = new TLVector<>();
    }

    public TopPeerCategoryPeers(TLTopPeerCategory category, int count, TLVector<TLTopPeer> peers) {
        this.category = category;
        this.count = count;
        this.peers = peers;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        category = (TLTopPeerCategory) buffer.readTLObject(APIContext.getInstance());
        count = buffer.readInt();
        peers = (TLVector<TLTopPeer>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(category);
        buff.writeInt(count);
        buff.writeTLObject(peers);
    }


    public int getConstructor() {
        return ID;
    }
}