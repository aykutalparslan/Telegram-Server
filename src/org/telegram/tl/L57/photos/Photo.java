package org.telegram.tl.L57.photos;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class Photo extends org.telegram.tl.photos.TLPhoto {

    public static final int ID = 0x20212ca8;

    public org.telegram.tl.photos.TLPhoto photo;
    public TLVector<org.telegram.tl.TLUser> users;

    public Photo() {
        this.users = new TLVector<>();
    }

    public Photo(org.telegram.tl.photos.TLPhoto photo, TLVector<org.telegram.tl.TLUser> users) {
        this.photo = photo;
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        photo = (org.telegram.tl.photos.TLPhoto) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(photo);
        buff.writeTLObject(users);
    }


    public int getConstructor() {
        return ID;
    }
}