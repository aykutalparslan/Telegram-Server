package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class DocumentL12 extends TLDocument {

    public static final int ID = 0x9efc6326;

    public long id;
    public long access_hash;
    public int user_id;
    public int date;
    public String file_name;
    public String mime_type;
    public int size;
    public TLPhotoSize thumb;
    public int dc_id;

    public DocumentL12() {
    }

    public DocumentL12(long id, long access_hash, int user_id, int date, String file_name, String mime_type, int size, TLPhotoSize thumb, int dc_id) {
        this.id = id;
        this.access_hash = access_hash;
        this.user_id = user_id;
        this.date = date;
        this.file_name = file_name;
        this.mime_type = mime_type;
        this.size = size;
        this.thumb = thumb;
        this.dc_id = dc_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readLong();
        access_hash = buffer.readLong();
        user_id = buffer.readInt();
        date = buffer.readInt();
        file_name = buffer.readString();
        mime_type = buffer.readString();
        size = buffer.readInt();
        thumb = (TLPhotoSize) buffer.readTLObject(APIContext.getInstance());
        dc_id = buffer.readInt();
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
        buff.writeInt(user_id);
        buff.writeInt(date);
        buff.writeString(file_name);
        buff.writeString(mime_type);
        buff.writeInt(size);
        buff.writeTLObject(thumb);
        buff.writeInt(dc_id);
    }

    public int getConstructor() {
        return ID;
    }
}