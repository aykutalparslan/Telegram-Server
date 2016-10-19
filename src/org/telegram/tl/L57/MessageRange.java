package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class MessageRange extends org.telegram.tl.TLMessageRange {

    public static final int ID = 0xae30253;

    public int min_id;
    public int max_id;

    public MessageRange() {
    }

    public MessageRange(int min_id, int max_id) {
        this.min_id = min_id;
        this.max_id = max_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        min_id = buffer.readInt();
        max_id = buffer.readInt();
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
        buff.writeInt(min_id);
        buff.writeInt(max_id);
    }


    public int getConstructor() {
        return ID;
    }
}