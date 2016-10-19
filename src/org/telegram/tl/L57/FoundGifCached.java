package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class FoundGifCached extends org.telegram.tl.TLFoundGif {

    public static final int ID = 0x9c750409;

    public String url;
    public org.telegram.tl.photos.TLPhoto photo;
    public org.telegram.tl.TLDocument document;

    public FoundGifCached() {
    }

    public FoundGifCached(String url, org.telegram.tl.photos.TLPhoto photo, org.telegram.tl.TLDocument document) {
        this.url = url;
        this.photo = photo;
        this.document = document;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        url = buffer.readString();
        photo = (org.telegram.tl.photos.TLPhoto) buffer.readTLObject(APIContext.getInstance());
        document = (org.telegram.tl.TLDocument) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(28);
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