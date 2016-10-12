package org.telegram.tl.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class RecentStickers extends TLRecentStickers {

    public static final int ID = 0x5ce20970;

    public int hash;
    public TLVector<TLDocument> stickers;

    public RecentStickers() {
        this.stickers = new TLVector<>();
    }

    public RecentStickers(int hash, TLVector<TLDocument> stickers) {
        this.hash = hash;
        this.stickers = stickers;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        hash = buffer.readInt();
        stickers = (TLVector<TLDocument>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(stickers);
    }


    public int getConstructor() {
        return ID;
    }
}