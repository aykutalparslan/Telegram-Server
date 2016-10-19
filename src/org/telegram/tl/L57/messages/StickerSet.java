package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class StickerSet extends org.telegram.tl.TLStickerSet {

    public static final int ID = 0xb60a24a6;

    public org.telegram.tl.TLStickerSet set;
    public TLVector<org.telegram.tl.TLStickerPack> packs;
    public TLVector<org.telegram.tl.TLDocument> documents;

    public StickerSet() {
        this.packs = new TLVector<>();
        this.documents = new TLVector<>();
    }

    public StickerSet(org.telegram.tl.TLStickerSet set, TLVector<org.telegram.tl.TLStickerPack> packs, TLVector<org.telegram.tl.TLDocument> documents) {
        this.set = set;
        this.packs = packs;
        this.documents = documents;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        set = (org.telegram.tl.TLStickerSet) buffer.readTLObject(APIContext.getInstance());
        packs = (TLVector<org.telegram.tl.TLStickerPack>) buffer.readTLObject(APIContext.getInstance());
        documents = (TLVector<org.telegram.tl.TLDocument>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(28);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeTLObject(set);
        buff.writeTLObject(packs);
        buff.writeTLObject(documents);
    }


    public int getConstructor() {
        return ID;
    }
}