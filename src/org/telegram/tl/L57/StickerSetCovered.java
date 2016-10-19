package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class StickerSetCovered extends org.telegram.tl.TLStickerSetCovered {

    public static final int ID = 0x6410a5d2;

    public org.telegram.tl.TLStickerSet set;
    public org.telegram.tl.TLDocument cover;

    public StickerSetCovered() {
    }

    public StickerSetCovered(org.telegram.tl.TLStickerSet set, org.telegram.tl.TLDocument cover) {
        this.set = set;
        this.cover = cover;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        set = (org.telegram.tl.TLStickerSet) buffer.readTLObject(APIContext.getInstance());
        cover = (org.telegram.tl.TLDocument) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(cover);
    }


    public int getConstructor() {
        return ID;
    }
}