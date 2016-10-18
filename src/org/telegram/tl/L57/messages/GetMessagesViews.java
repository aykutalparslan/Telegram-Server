package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class GetMessagesViews extends TLObject {

    public static final int ID = 0xc4c8a55d;

    public TLInputPeer peer;
    public TLVector<Integer> id;
    public boolean increment;

    public GetMessagesViews() {
        this.id = new TLVector<>();
    }

    public GetMessagesViews(TLInputPeer peer, TLVector<Integer> id, boolean increment) {
        this.peer = peer;
        this.id = id;
        this.increment = increment;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        id = (TLVector<Integer>) buffer.readTLObject(APIContext.getInstance());
        increment = buffer.readBool();
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
        buff.writeTLObject(id);
        buff.writeBool(increment);
    }


    public int getConstructor() {
        return ID;
    }
}