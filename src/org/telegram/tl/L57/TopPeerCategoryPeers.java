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