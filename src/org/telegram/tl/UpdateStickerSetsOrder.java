package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class UpdateStickerSetsOrder extends TLUpdate {

    public static final int ID = 0xbb2d201;

    public int flags;
    public TLVector<Long> order;

    public UpdateStickerSetsOrder() {
        this.order = new TLVector<>();
    }

    public UpdateStickerSetsOrder(int flags, TLVector<Long> order) {
        this.flags = flags;
        this.order = order;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        order = (TLVector<Long>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(32);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeTLObject(order);
    }

    public boolean is_updateStickerSetsOrder_masks() {
        return (flags & (1 << 0)) != 0;
    }

    public boolean set_updateStickerSetsOrder_masks() {
        return (flags |= (1 << 0)) != 0;
    }

    public int getConstructor() {
        return ID;
    }
}