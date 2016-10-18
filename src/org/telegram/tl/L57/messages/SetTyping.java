package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SetTyping extends TLObject {

    public static final int ID = 0xa3825e50;

    public TLInputPeer peer;
    public TLSendMessageAction action;

    public SetTyping() {
    }

    public SetTyping(TLInputPeer peer, TLSendMessageAction action) {
        this.peer = peer;
        this.action = action;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        action = (TLSendMessageAction) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(action);
    }


    public int getConstructor() {
        return ID;
    }
}