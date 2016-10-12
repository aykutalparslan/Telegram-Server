package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class InputStickeredMediaDocument extends TLInputStickeredMedia {

    public static final int ID = 0x438865b;

    public TLInputDocument id;

    public InputStickeredMediaDocument() {
    }

    public InputStickeredMediaDocument(TLInputDocument id) {
        this.id = id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = (TLInputDocument) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(id);
    }


    public int getConstructor() {
        return ID;
    }
}