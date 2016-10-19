package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class NotifyPeer extends org.telegram.tl.TLNotifyPeer {

    public static final int ID = 0x9fd40bd8;

    public org.telegram.tl.TLPeer peer;

    public NotifyPeer() {
    }

    public NotifyPeer(org.telegram.tl.TLPeer peer) {
        this.peer = peer;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (org.telegram.tl.TLPeer) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(12);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeTLObject(peer);
    }


    public int getConstructor() {
        return ID;
    }
}