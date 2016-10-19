package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateUserStatus extends org.telegram.tl.TLUpdate {

    public static final int ID = 0x1bfbd823;

    public int user_id;
    public org.telegram.tl.TLUserStatus status;

    public UpdateUserStatus() {
    }

    public UpdateUserStatus(int user_id, org.telegram.tl.TLUserStatus status) {
        this.user_id = user_id;
        this.status = status;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        user_id = buffer.readInt();
        status = (org.telegram.tl.TLUserStatus) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(16);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(user_id);
        buff.writeTLObject(status);
    }


    public int getConstructor() {
        return ID;
    }
}