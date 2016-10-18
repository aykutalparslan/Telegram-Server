package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class AllStickers extends TLAllStickers {

    public static final int ID = 0xedfd405f;

    public int hash;
    public TLVector<TLStickerSet> sets;

    public AllStickers() {
        this.sets = new TLVector<>();
    }

    public AllStickers(int hash, TLVector<TLStickerSet> sets) {
        this.hash = hash;
        this.sets = sets;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        hash = buffer.readInt();
        sets = (TLVector<TLStickerSet>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeInt(hash);
        buff.writeTLObject(sets);
    }


    public int getConstructor() {
        return ID;
    }
}