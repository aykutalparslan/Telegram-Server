package org.telegram.tl.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class ArchivedStickers extends TLArchivedStickers {

    public static final int ID = 0x4fcba9c8;

    public int count;
    public TLVector<TLStickerSetCovered> sets;

    public ArchivedStickers() {
        this.sets = new TLVector<>();
    }

    public ArchivedStickers(int count, TLVector<TLStickerSetCovered> sets) {
        this.count = count;
        this.sets = sets;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        count = buffer.readInt();
        sets = (TLVector<TLStickerSetCovered>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeInt(count);
        buff.writeTLObject(sets);
    }


    public int getConstructor() {
        return ID;
    }
}