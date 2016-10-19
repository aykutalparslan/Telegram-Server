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