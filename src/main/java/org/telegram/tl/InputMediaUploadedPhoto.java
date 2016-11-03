package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class InputMediaUploadedPhoto extends TLInputMedia {

    public static final int ID = 0xf7aff1c0;

    public TLInputFile file;
    public String caption;

    public InputMediaUploadedPhoto() {
    }

    public InputMediaUploadedPhoto(TLInputFile file, String caption) {
        this.file = file;
        this.caption = caption;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        file = (TLInputFile) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(file);
        buff.writeString(caption);
    }

    public int getConstructor() {
        return ID;
    }
}