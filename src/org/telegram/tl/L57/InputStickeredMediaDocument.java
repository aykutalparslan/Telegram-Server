package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputStickeredMediaDocument extends org.telegram.tl.TLInputStickeredMedia {

    public static final int ID = 0x438865b;

    public org.telegram.tl.TLInputDocument id;

    public InputStickeredMediaDocument() {
    }

    public InputStickeredMediaDocument(org.telegram.tl.TLInputDocument id) {
        this.id = id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = (org.telegram.tl.TLInputDocument) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(id);
    }


    public int getConstructor() {
        return ID;
    }
}