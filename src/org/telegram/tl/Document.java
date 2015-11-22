package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class Document extends TLDocument {

    public static final int ID = 0xf9a39f4f;

    public long id;
    public long access_hash;
    public int date;
    public String mime_type;
    public int size;
    public TLPhotoSize thumb;
    public int dc_id;
    public TLVector<TLDocumentAttribute> attributes;

    public Document() {
        this.attributes = new TLVector<>();
    }

    public Document(long id, long access_hash, int date, String mime_type, int size, TLPhotoSize thumb, int dc_id, TLVector<TLDocumentAttribute> attributes){
        this.id = id;
        this.access_hash = access_hash;
        this.date = date;
        this.mime_type = mime_type;
        this.size = size;
        this.thumb = thumb;
        this.dc_id = dc_id;
        this.attributes = attributes;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readLong();
        access_hash = buffer.readLong();
        date = buffer.readInt();
        mime_type = buffer.readString();
        size = buffer.readInt();
        thumb = (TLPhotoSize) buffer.readTLObject(APIContext.getInstance());
        dc_id = buffer.readInt();
        attributes = (TLVector<TLDocumentAttribute>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeLong(id);
        buff.writeLong(access_hash);
        buff.writeInt(date);
        buff.writeString(mime_type);
        buff.writeInt(size);
        buff.writeTLObject(thumb);
        buff.writeInt(dc_id);
        buff.writeTLObject(attributes);
    }

    public int getConstructor() {
        return ID;
    }
}