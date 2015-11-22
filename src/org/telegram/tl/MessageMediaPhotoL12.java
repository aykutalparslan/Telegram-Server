package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class MessageMediaPhotoL12 extends TLMessageMedia {

    public static final int ID = 0xc8c45a2a;

    public TLPhoto photo;

    public MessageMediaPhotoL12() {
    }

    public MessageMediaPhotoL12(TLPhoto photo) {
        this.photo = photo;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        photo = (TLPhoto) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(photo);
    }

    public int getConstructor() {
        return ID;
    }
}