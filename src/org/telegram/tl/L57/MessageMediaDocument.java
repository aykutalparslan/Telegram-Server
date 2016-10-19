package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class MessageMediaDocument extends org.telegram.tl.TLMessageMedia {

    public static final int ID = 0xf3e02ea8;

    public org.telegram.tl.TLDocument document;
    public String caption;

    public MessageMediaDocument() {
    }

    public MessageMediaDocument(org.telegram.tl.TLDocument document, String caption) {
        this.document = document;
        this.caption = caption;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        document = (org.telegram.tl.TLDocument) buffer.readTLObject(APIContext.getInstance());
        caption = buffer.readString();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(20);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeTLObject(document);
        buff.writeString(caption);
    }


    public int getConstructor() {
        return ID;
    }
}