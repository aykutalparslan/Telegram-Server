package org.telegram.tl.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class GetStickerSet extends TLObject {

    public static final int ID = 0x2619a90e;

    public TLInputStickerSet stickerset;

    public GetStickerSet() {
    }

    public GetStickerSet(TLInputStickerSet stickerset) {
        this.stickerset = stickerset;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        stickerset = (TLInputStickerSet) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(stickerset);
    }


    public int getConstructor() {
        return ID;
    }
}