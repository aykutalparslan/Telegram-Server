package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UserProfilePhoto extends org.telegram.tl.TLUserProfilePhoto {

    public static final int ID = 0xd559d8c8;

    public long photo_id;
    public org.telegram.tl.TLFileLocation photo_small;
    public org.telegram.tl.TLFileLocation photo_big;

    public UserProfilePhoto() {
    }

    public UserProfilePhoto(long photo_id, org.telegram.tl.TLFileLocation photo_small, org.telegram.tl.TLFileLocation photo_big) {
        this.photo_id = photo_id;
        this.photo_small = photo_small;
        this.photo_big = photo_big;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        photo_id = buffer.readLong();
        photo_small = (org.telegram.tl.TLFileLocation) buffer.readTLObject(APIContext.getInstance());
        photo_big = (org.telegram.tl.TLFileLocation) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeLong(photo_id);
        buff.writeTLObject(photo_small);
        buff.writeTLObject(photo_big);
    }


    public int getConstructor() {
        return ID;
    }
}