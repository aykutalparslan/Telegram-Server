package org.telegram.tl.L57.photos;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class Photos extends org.telegram.tl.photos.TLPhotos {

    public static final int ID = 0x8dca6aa5;

    public TLVector<org.telegram.tl.TLPhoto> photos;
    public TLVector<org.telegram.tl.TLUser> users;

    public Photos() {
        this.photos = new TLVector<>();
        this.users = new TLVector<>();
    }

    public Photos(TLVector<org.telegram.tl.TLPhoto> photos, TLVector<org.telegram.tl.TLUser> users) {
        this.photos = photos;
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        photos = (TLVector<org.telegram.tl.TLPhoto>) buffer.readTLObject(APIContext.getInstance());
        users = (TLVector<org.telegram.tl.TLUser>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(20);
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