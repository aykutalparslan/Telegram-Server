package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateMessageID extends org.telegram.tl.TLUpdate {

    public static final int ID = 0x4e90bfd6;

    public int id;
    public long random_id;

    public UpdateMessageID() {
    }

    public UpdateMessageID(int id, long random_id) {
        this.id = id;
        this.random_id = random_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readInt();
        random_id = buffer.readLong();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(16);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(id);
        buff.writeLong(random_id);
    }


    public int getConstructor() {
        return ID;
    }
}