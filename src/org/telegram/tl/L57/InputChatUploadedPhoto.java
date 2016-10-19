package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputChatUploadedPhoto extends org.telegram.tl.TLInputChatPhoto {

    public static final int ID = 0x927c55b4;

    public org.telegram.tl.TLInputFile file;

    public InputChatUploadedPhoto() {
    }

    public InputChatUploadedPhoto(org.telegram.tl.TLInputFile file) {
        this.file = file;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        file = (org.telegram.tl.TLInputFile) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(file);
    }


    public int getConstructor() {
        return ID;
    }
}