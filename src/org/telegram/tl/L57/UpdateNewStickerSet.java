package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;
import org.telegram.tl.messages.*;

public class UpdateNewStickerSet extends org.telegram.tl.TLUpdate {

    public static final int ID = 0x688a30aa;

    public org.telegram.tl.TLStickerSet stickerset;

    public UpdateNewStickerSet() {
    }

    public UpdateNewStickerSet(org.telegram.tl.TLStickerSet stickerset) {
        this.stickerset = stickerset;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        stickerset = (org.telegram.tl.TLStickerSet) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(stickerset);
    }


    public int getConstructor() {
        return ID;
    }
}