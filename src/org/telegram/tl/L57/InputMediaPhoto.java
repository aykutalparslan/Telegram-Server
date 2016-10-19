package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputMediaPhoto extends org.telegram.tl.TLInputMedia {

    public static final int ID = 0xe9bfb4f3;

    public org.telegram.tl.TLInputPhoto id;
    public String caption;

    public InputMediaPhoto() {
    }

    public InputMediaPhoto(org.telegram.tl.TLInputPhoto id, String caption) {
        this.id = id;
        this.caption = caption;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = (org.telegram.tl.TLInputPhoto) buffer.readTLObject(APIContext.getInstance());
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