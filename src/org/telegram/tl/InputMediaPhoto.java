package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class InputMediaPhoto extends TLInputMedia {

    public static final int ID = 0xe9bfb4f3;

    public TLInputPhoto id;
    public String caption;

    public InputMediaPhoto() {
    }

    public InputMediaPhoto(TLInputPhoto id, String caption) {
        this.id = id;
        this.caption = caption;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = (TLInputPhoto) buffer.readTLObject(APIContext.getInstance());
        caption = buffer.readString();
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
        buff.writeString(caption);
    }

    public int getConstructor() {
        return ID;
    }
}