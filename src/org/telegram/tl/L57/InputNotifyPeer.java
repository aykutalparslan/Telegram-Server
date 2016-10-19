package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputNotifyPeer extends org.telegram.tl.TLInputNotifyPeer {

    public static final int ID = 0xb8bc5b0c;

    public org.telegram.tl.TLInputPeer peer;

    public InputNotifyPeer() {
    }

    public InputNotifyPeer(org.telegram.tl.TLInputPeer peer) {
        this.peer = peer;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (org.telegram.tl.TLInputPeer) buffer.readTLObject(APIContext.getInstance());
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