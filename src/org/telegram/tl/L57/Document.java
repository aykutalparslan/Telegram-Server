package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class Document extends org.telegram.tl.TLDocument {

    public static final int ID = 0x87232bc7;

    public long id;
    public long access_hash;
    public int date;
    public String mime_type;
    public int size;
    public org.telegram.tl.TLPhotoSize thumb;
    public int dc_id;
    public int version;
    public TLVector<org.telegram.tl.TLDocumentAttribute> attributes;

    public Document() {
        this.attributes = new TLVector<>();
    }

    public Document(long id, long access_hash, int date, String mime_type, int size, org.telegram.tl.TLPhotoSize thumb, int dc_id, int version, TLVector<org.telegram.tl.TLDocumentAttribute> attributes) {
        this.id = id;
        this.access_hash = access_hash;
        this.date = date;
        this.mime_type = mime_type;
        this.size = size;
        this.thumb = thumb;
        this.dc_id = dc_id;
        this.version = version;
        this.attributes = attributes;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readLong();
        access_hash = buffer.readLong();
        date = buffer.readInt();
        mime_type = buffer.readString();
        size = buffer.readInt();
        thumb = (org.telegram.tl.TLPhotoSize) buffer.readTLObject(APIContext.getInstance());
        dc_id = buffer.readInt();
        version = buffer.readInt();
        attributes = (TLVector<org.telegram.tl.TLDocumentAttribute>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(60);
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
        buff.writeInt(version);
        buff.writeTLObject(attributes);
    }


    public int getConstructor() {
        return ID;
    }
}