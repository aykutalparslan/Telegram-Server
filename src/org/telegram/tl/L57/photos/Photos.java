package org.telegram.tl.L57.photos;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class Photos extends TLPhotos {

    public static final int ID = 0x8dca6aa5;

    public TLVector<TLPhoto> photos;
    public TLVector<TLUser> users;

    public Photos() {
        this.photos = new TLVector<>();
        this.users = new TLVector<>();
    }

    public Photos(TLVector<TLPhoto> photos, TLVector<TLUser> users) {
        this.photos = photos;
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        photos = (TLVector<TLPhoto>) buffer.readTLObject(APIContext.getInstance());
        users = (TLVector<TLUser>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(photos);
        buff.writeTLObject(users);
    }


    public int getConstructor() {
        return ID;
    }
}