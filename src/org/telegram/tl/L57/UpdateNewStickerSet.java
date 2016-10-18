package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;
import org.telegram.tl.messages.*;

public class UpdateNewStickerSet extends TLUpdate {

    public static final int ID = 0x688a30aa;

    public TLStickerSet stickerset;

    public UpdateNewStickerSet() {
    }

    public UpdateNewStickerSet(TLStickerSet stickerset) {
        this.stickerset = stickerset;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        stickerset = (TLStickerSet) buffer.readTLObject(APIContext.getInstance());
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