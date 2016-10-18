package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class StickerSetMultiCovered extends TLStickerSetCovered {

    public static final int ID = 0x3407e51b;

    public TLStickerSet set;
    public TLVector<TLDocument> covers;

    public StickerSetMultiCovered() {
        this.covers = new TLVector<>();
    }

    public StickerSetMultiCovered(TLStickerSet set, TLVector<TLDocument> covers) {
        this.set = set;
        this.covers = covers;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        set = (TLStickerSet) buffer.readTLObject(APIContext.getInstance());
        covers = (TLVector<TLDocument>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(set);
        buff.writeTLObject(covers);
    }


    public int getConstructor() {
        return ID;
    }
}