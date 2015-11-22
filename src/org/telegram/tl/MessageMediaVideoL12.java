package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class MessageMediaVideoL12 extends TLMessageMedia {

    public static final int ID = 0xa2d24290;

    public TLVideo video;

    public MessageMediaVideoL12() {
    }

    public MessageMediaVideoL12(TLVideo video) {
        this.video = video;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        video = (TLVideo) buffer.readTLObject(APIContext.getInstance());
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
    }

    public int getConstructor() {
        return ID;
    }
}