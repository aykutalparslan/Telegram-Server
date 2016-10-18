package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class StickerPack extends TLStickerPack {

    public static final int ID = 0x12b299d4;

    public String emoticon;
    public TLVector<Long> documents;

    public StickerPack() {
        this.documents = new TLVector<>();
    }

    public StickerPack(String emoticon, TLVector<Long> documents) {
        this.emoticon = emoticon;
        this.documents = documents;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        emoticon = buffer.readString();
        documents = (TLVector<Long>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeString(emoticon);
        buff.writeTLObject(documents);
    }


    public int getConstructor() {
        return ID;
    }
}