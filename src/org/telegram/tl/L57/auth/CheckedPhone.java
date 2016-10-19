package org.telegram.tl.L57.auth;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class CheckedPhone extends org.telegram.tl.auth.TLCheckedPhone {

    public static final int ID = 0x811ea28e;

    public boolean phone_registered;

    public CheckedPhone() {
    }

    public CheckedPhone(boolean phone_registered) {
        this.phone_registered = phone_registered;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        phone_registered = buffer.readBool();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(5);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeBool(phone_registered);
    }


    public int getConstructor() {
        return ID;
    }
}