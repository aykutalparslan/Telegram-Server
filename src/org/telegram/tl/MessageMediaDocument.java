package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class MessageMediaDocument extends TLMessageMedia {

    public static final int ID = 0x2fda2204;

    public TLDocument document;

    public MessageMediaDocument() {
    }

    public MessageMediaDocument(TLDocument document){
        this.document = document;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        document = (TLDocument) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(document);
    }

    public int getConstructor() {
        return ID;
    }
}