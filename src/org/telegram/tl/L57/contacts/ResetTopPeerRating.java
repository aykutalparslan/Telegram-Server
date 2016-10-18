package org.telegram.tl.L57.contacts;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ResetTopPeerRating extends TLObject {

    public static final int ID = 0x1ae373ac;

    public TLTopPeerCategory category;
    public TLInputPeer peer;

    public ResetTopPeerRating() {
    }

    public ResetTopPeerRating(TLTopPeerCategory category, TLInputPeer peer) {
        this.category = category;
        this.peer = peer;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        category = (TLTopPeerCategory) buffer.readTLObject(APIContext.getInstance());
        peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(peer);
    }


    public int getConstructor() {
        return ID;
    }
}