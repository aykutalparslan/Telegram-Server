package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class FeaturedStickers extends org.telegram.tl.messages.TLFeaturedStickers {

    public static final int ID = 0xf89d88e5;

    public int hash;
    public TLVector<org.telegram.tl.TLStickerSetCovered> sets;
    public TLVector<Long> unread;

    public FeaturedStickers() {
        this.sets = new TLVector<>();
        this.unread = new TLVector<>();
    }

    public FeaturedStickers(int hash, TLVector<org.telegram.tl.TLStickerSetCovered> sets, TLVector<Long> unread) {
        this.hash = hash;
        this.sets = sets;
        this.unread = unread;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        hash = buffer.readInt();
        sets = (TLVector<org.telegram.tl.TLStickerSetCovered>) buffer.readTLObject(APIContext.getInstance());
        unread = (TLVector<Long>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(24);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(hash);
        buff.writeTLObject(sets);
        buff.writeTLObject(unread);
    }


    public int getConstructor() {
        return ID;
    }
}