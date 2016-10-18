package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ReceivedQueue extends TLObject {

    public static final int ID = 0x55a5bb66;

    public int max_qts;

    public ReceivedQueue() {
    }

    public ReceivedQueue(int max_qts) {
        this.max_qts = max_qts;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        max_qts = buffer.readInt();
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
        buff.writeInt(max_qts);
    }


    public int getConstructor() {
        return ID;
    }
}