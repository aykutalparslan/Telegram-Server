package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

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