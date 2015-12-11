package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class InputMediaUploadedVideo extends TLInputMedia {

    public static final int ID = 0xe13fd4bc;

    public TLInputFile file;
    public int duration;
    public int w;
    public int h;
    public String caption;

    public InputMediaUploadedVideo() {
    }

    public InputMediaUploadedVideo(TLInputFile file, int duration, int w, int h, String caption) {
        this.file = file;
        this.duration = duration;
        this.w = w;
        this.h = h;
        this.caption = caption;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        file = (TLInputFile) buffer.readTLObject(APIContext.getInstance());
        duration = buffer.readInt();
        w = buffer.readInt();
        h = buffer.readInt();
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
        buff.writeInt(duration);
        buff.writeInt(w);
        buff.writeInt(h);
        buff.writeString(caption);
    }

    public int getConstructor() {
        return ID;
    }
}