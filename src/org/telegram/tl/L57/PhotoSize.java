package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class PhotoSize extends org.telegram.tl.TLPhotoSize {

    public static final int ID = 0x77bfb61b;

    public String type;
    public org.telegram.tl.TLFileLocation location;
    public int w;
    public int h;
    public int size;

    public PhotoSize() {
    }

    public PhotoSize(String type, org.telegram.tl.TLFileLocation location, int w, int h, int size) {
        this.type = type;
        this.location = location;
        this.w = w;
        this.h = h;
        this.size = size;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        type = buffer.readString();
        location = (org.telegram.tl.TLFileLocation) buffer.readTLObject(APIContext.getInstance());
        w = buffer.readInt();
        h = buffer.readInt();
        size = buffer.readInt();
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
        buff.writeString(type);
        buff.writeTLObject(location);
        buff.writeInt(w);
        buff.writeInt(h);
        buff.writeInt(size);
    }


    public int getConstructor() {
        return ID;
    }
}