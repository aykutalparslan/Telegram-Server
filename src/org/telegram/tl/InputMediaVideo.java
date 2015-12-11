package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class InputMediaVideo extends TLInputMedia {

    public static final int ID = 0x936a4ebd;

    public TLInputVideo video_id;
    public String caption;

    public InputMediaVideo() {
    }

    public InputMediaVideo(TLInputVideo video_id, String caption) {
        this.video_id = video_id;
        this.caption = caption;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        video_id = (TLInputVideo) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(video_id);
        buff.writeString(caption);
    }

    public int getConstructor() {
        return ID;
    }
}