package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class PhotoCachedSize extends TLPhotoSize {

    public static final int ID = 0xe9a734fa;

    public String type;
    public TLFileLocation location;
    public int w;
    public int h;
    public byte[] bytes;

    public PhotoCachedSize() {
    }

    public PhotoCachedSize(String type, TLFileLocation location, int w, int h, byte[] bytes) {
        this.type = type;
        this.location = location;
        this.w = w;
        this.h = h;
        this.bytes = bytes;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        type = buffer.readString();
        location = (TLFileLocation) buffer.readTLObject(APIContext.getInstance());
        w = buffer.readInt();
        h = buffer.readInt();
        bytes = buffer.readBytes();
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
        buff.writeBytes(bytes);
    }


    public int getConstructor() {
        return ID;
    }
}