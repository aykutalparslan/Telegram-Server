package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ForwardMessage extends TLObject {

    public static final int ID = 0x33963bf9;

    public TLInputPeer peer;
    public int id;
    public long random_id;

    public ForwardMessage() {
    }

    public ForwardMessage(TLInputPeer peer, int id, long random_id) {
        this.peer = peer;
        this.id = id;
        this.random_id = random_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        id = buffer.readInt();
        random_id = buffer.readLong();
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
        buff.writeInt(id);
        buff.writeLong(random_id);
    }


    public int getConstructor() {
        return ID;
    }
}