package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class GetPeerDialogs extends TLObject {

    public static final int ID = 0x2d9776b9;

    public TLVector<TLInputPeer> peers;

    public GetPeerDialogs() {
        this.peers = new TLVector<>();
    }

    public GetPeerDialogs(TLVector<TLInputPeer> peers) {
        this.peers = peers;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peers = (TLVector<TLInputPeer>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(peers);
    }


    public int getConstructor() {
        return ID;
    }
}