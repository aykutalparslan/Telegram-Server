package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class MessageMediaPhoto extends TLMessageMedia {

    public static final int ID = 0x3d8ce53d;

    public TLPhoto photo;
    public String caption;

    public MessageMediaPhoto() {
    }

    public MessageMediaPhoto(TLPhoto photo, String caption) {
        this.photo = photo;
        this.caption = caption;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        photo = (TLPhoto) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(photo);
        buff.writeString(caption);
    }

    public int getConstructor() {
        return ID;
    }
}