package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputMediaDocument extends org.telegram.tl.TLInputMedia {

    public static final int ID = 0x1a77f29c;

    public org.telegram.tl.TLInputDocument id;
    public String caption;

    public InputMediaDocument() {
    }

    public InputMediaDocument(org.telegram.tl.TLInputDocument id, String caption) {
        this.id = id;
        this.caption = caption;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = (org.telegram.tl.TLInputDocument) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(id);
        buff.writeString(caption);
    }


    public int getConstructor() {
        return ID;
    }
}