package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class RecentStickers extends org.telegram.tl.messages.TLRecentStickers {

    public static final int ID = 0x5ce20970;

    public int hash;
    public TLVector<org.telegram.tl.TLDocument> stickers;

    public RecentStickers() {
        this.stickers = new TLVector<>();
    }

    public RecentStickers(int hash, TLVector<org.telegram.tl.TLDocument> stickers) {
        this.hash = hash;
        this.stickers = stickers;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        hash = buffer.readInt();
        stickers = (TLVector<org.telegram.tl.TLDocument>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(16);
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