package org.telegram.tl.L57.auth;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class PasswordRecovery extends org.telegram.tl.auth.TLPasswordRecovery {

    public static final int ID = 0x137948a5;

    public String email_pattern;

    public PasswordRecovery() {
    }

    public PasswordRecovery(String email_pattern) {
        this.email_pattern = email_pattern;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        email_pattern = buffer.readString();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(12);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeString(email_pattern);
    }


    public int getConstructor() {
        return ID;
    }
}