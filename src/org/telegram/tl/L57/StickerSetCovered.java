package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class StickerSetCovered extends TLStickerSetCovered {

    public static final int ID = 0x6410a5d2;

    public TLStickerSet set;
    public TLDocument cover;

    public StickerSetCovered() {
    }

    public StickerSetCovered(TLStickerSet set, TLDocument cover) {
        this.set = set;
        this.cover = cover;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        set = (TLStickerSet) buffer.readTLObject(APIContext.getInstance());
        cover = (TLDocument) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(cover);
    }


    public int getConstructor() {
        return ID;
    }
}