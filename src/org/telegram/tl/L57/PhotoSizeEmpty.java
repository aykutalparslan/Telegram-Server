package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class PhotoSizeEmpty extends org.telegram.tl.TLPhotoSize {

    public static final int ID = 0xe17e23c;

    public String type;

    public PhotoSizeEmpty() {
    }

    public PhotoSizeEmpty(String type) {
        this.type = type;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        type = buffer.readString();
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
        buff.writeString(type);
    }


    public int getConstructor() {
        return ID;
    }
}