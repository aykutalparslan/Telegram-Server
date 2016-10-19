package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class MessageEntityCode extends org.telegram.tl.TLMessageEntity {

    public static final int ID = 0x28a20571;

    public int offset;
    public int length;

    public MessageEntityCode() {
    }

    public MessageEntityCode(int offset, int length) {
        this.offset = offset;
        this.length = length;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        offset = buffer.readInt();
        length = buffer.readInt();
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
        buff.writeInt(offset);
        buff.writeInt(length);
    }


    public int getConstructor() {
        return ID;
    }
}