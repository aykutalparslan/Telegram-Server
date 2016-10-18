package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateUserPhoto extends TLUpdate {

    public static final int ID = 0x95313b0c;

    public int user_id;
    public int date;
    public TLUserProfilePhoto photo;
    public boolean previous;

    public UpdateUserPhoto() {
    }

    public UpdateUserPhoto(int user_id, int date, TLUserProfilePhoto photo, boolean previous) {
        this.user_id = user_id;
        this.date = date;
        this.photo = photo;
        this.previous = previous;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        user_id = buffer.readInt();
        date = buffer.readInt();
        photo = (TLUserProfilePhoto) buffer.readTLObject(APIContext.getInstance());
        previous = buffer.readBool();
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
        buff.writeInt(user_id);
        buff.writeInt(date);
        buff.writeTLObject(photo);
        buff.writeBool(previous);
    }


    public int getConstructor() {
        return ID;
    }
}