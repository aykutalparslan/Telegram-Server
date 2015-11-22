package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class PhotoL12 extends TLPhoto {

    public static final int ID = 0x22b56751;

    public long id;
    public long access_hash;
    public int user_id;
    public int date;
    public String caption;
    public TLGeoPoint geo;
    public TLVector<TLPhotoSize> sizes;

    public PhotoL12() {
        this.sizes = new TLVector<>();
    }

    public PhotoL12(long id, long access_hash, int user_id, int date, String caption, TLGeoPoint geo, TLVector<TLPhotoSize> sizes) {
        this.id = id;
        this.access_hash = access_hash;
        this.user_id = user_id;
        this.date = date;
        this.caption = caption;
        this.geo = geo;
        this.sizes = sizes;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readLong();
        access_hash = buffer.readLong();
        user_id = buffer.readInt();
        date = buffer.readInt();
        caption = buffer.readString();
        geo = (TLGeoPoint) buffer.readTLObject(APIContext.getInstance());
        sizes = (TLVector<TLPhotoSize>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeString(caption);
        buff.writeTLObject(geo);
        buff.writeTLObject(sizes);
    }

    public int getConstructor() {
        return ID;
    }
}