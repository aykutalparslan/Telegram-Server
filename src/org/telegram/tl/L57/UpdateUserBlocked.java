package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateUserBlocked extends org.telegram.tl.TLUpdate {

    public static final int ID = 0x80ece81a;

    public int user_id;
    public boolean blocked;

    public UpdateUserBlocked() {
    }

    public UpdateUserBlocked(int user_id, boolean blocked) {
        this.user_id = user_id;
        this.blocked = blocked;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        user_id = buffer.readInt();
        blocked = buffer.readBool();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(9);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(user_id);
        buff.writeBool(blocked);
    }


    public int getConstructor() {
        return ID;
    }
}