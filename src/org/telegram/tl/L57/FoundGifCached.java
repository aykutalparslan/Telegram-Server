package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class FoundGifCached extends TLFoundGif {

    public static final int ID = 0x9c750409;

    public String url;
    public TLPhoto photo;
    public TLDocument document;

    public FoundGifCached() {
    }

    public FoundGifCached(String url, TLPhoto photo, TLDocument document) {
        this.url = url;
        this.photo = photo;
        this.document = document;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        url = buffer.readString();
        photo = (TLPhoto) buffer.readTLObject(APIContext.getInstance());
        document = (TLDocument) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeString(url);
        buff.writeTLObject(photo);
        buff.writeTLObject(document);
    }


    public int getConstructor() {
        return ID;
    }
}