package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class MessageMediaVideo extends TLMessageMedia {

    public static final int ID = 0x5bcf1675;

    public TLVideo video;
    public String caption;

    public MessageMediaVideo() {
    }

    public MessageMediaVideo(TLVideo video, String caption) {
        this.video = video;
        this.caption = caption;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        video = (TLVideo) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(video);
        buff.writeString(caption);
    }

    public int getConstructor() {
        return ID;
    }
}