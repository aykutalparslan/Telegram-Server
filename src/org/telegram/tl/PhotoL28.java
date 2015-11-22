package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class PhotoL28 extends TLPhoto {

    public static final int ID = 0xc3838076;

    public long id;
    public long access_hash;
    public int user_id;
    public int date;
    public TLGeoPoint geo;
    public TLVector<TLPhotoSize> sizes;

    public PhotoL28() {
        this.sizes = new TLVector<>();
    }

    public PhotoL28(long id, long access_hash, int user_id, int date, TLGeoPoint geo, TLVector<TLPhotoSize> sizes) {
        this.id = id;
        this.access_hash = access_hash;
        this.user_id = user_id;
        this.date = date;
        this.geo = geo;
        this.sizes = sizes;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readLong();
        access_hash = buffer.readLong();
        user_id = buffer.readInt();
        date = buffer.readInt();
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
        buff.writeTLObject(geo);
        buff.writeTLObject(sizes);
    }

    public int getConstructor() {
        return ID;
    }
}