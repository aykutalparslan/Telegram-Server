package org.telegram.tl.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class StickerSet extends TLStickerSet {

    public static final int ID = 0xb60a24a6;

    public TLStickerSet set;
    public TLVector<TLStickerPack> packs;
    public TLVector<TLDocument> documents;

    public StickerSet() {
        this.packs = new TLVector<>();
        this.documents = new TLVector<>();
    }

    public StickerSet(TLStickerSet set, TLVector<TLStickerPack> packs, TLVector<TLDocument> documents) {
        this.set = set;
        this.packs = packs;
        this.documents = documents;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        set = (TLStickerSet) buffer.readTLObject(APIContext.getInstance());
        packs = (TLVector<TLStickerPack>) buffer.readTLObject(APIContext.getInstance());
        documents = (TLVector<TLDocument>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(packs);
        buff.writeTLObject(documents);
    }


    public int getConstructor() {
        return ID;
    }
}