package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class InputMediaUploadedAudio extends TLInputMedia {

    public static final int ID = 0x4e498cab;

    public TLInputFile file;
    public int duration;
    public String mime_type;

    public InputMediaUploadedAudio() {
    }

    public InputMediaUploadedAudio(TLInputFile file, int duration, String mime_type){
        this.file = file;
        this.duration = duration;
        this.mime_type = mime_type;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        file = (TLInputFile) buffer.readTLObject(APIContext.getInstance());
        duration = buffer.readInt();
        mime_type = buffer.readString();
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
        buff.writeInt(duration);
        buff.writeString(mime_type);
    }

    public int getConstructor() {
        return ID;
    }
}