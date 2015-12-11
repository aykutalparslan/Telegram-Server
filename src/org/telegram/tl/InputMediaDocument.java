package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class InputMediaDocument extends TLInputMedia {

    public static final int ID = 0xd184e841;

    public TLInputDocument document_id;

    public InputMediaDocument() {
    }

    public InputMediaDocument(TLInputDocument document_id) {
        this.document_id = document_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        document_id = (TLInputDocument) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(document_id);
    }

    public int getConstructor() {
        return ID;
    }
}