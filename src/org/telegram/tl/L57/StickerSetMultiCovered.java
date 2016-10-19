package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class StickerSetMultiCovered extends org.telegram.tl.TLStickerSetCovered {

    public static final int ID = 0x3407e51b;

    public org.telegram.tl.TLStickerSet set;
    public TLVector<org.telegram.tl.TLDocument> covers;

    public StickerSetMultiCovered() {
        this.covers = new TLVector<>();
    }

    public StickerSetMultiCovered(org.telegram.tl.TLStickerSet set, TLVector<org.telegram.tl.TLDocument> covers) {
        this.set = set;
        this.covers = covers;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        set = (org.telegram.tl.TLStickerSet) buffer.readTLObject(APIContext.getInstance());
        covers = (TLVector<org.telegram.tl.TLDocument>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(20);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeTLObject(set);
        buff.writeTLObject(covers);
    }


    public int getConstructor() {
        return ID;
    }
}