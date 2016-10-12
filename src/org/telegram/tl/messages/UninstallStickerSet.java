package org.telegram.tl.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class UninstallStickerSet extends TLObject {

    public static final int ID = 0xf96e55de;

    public TLInputStickerSet stickerset;

    public UninstallStickerSet() {
    }

    public UninstallStickerSet(TLInputStickerSet stickerset) {
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