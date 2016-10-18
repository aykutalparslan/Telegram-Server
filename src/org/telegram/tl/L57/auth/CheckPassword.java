package org.telegram.tl.L57.auth;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class CheckPassword extends TLObject {

    public static final int ID = 0xa63011e;

    public byte[] password_hash;

    public CheckPassword() {
    }

    public CheckPassword(byte[] password_hash) {
        this.password_hash = password_hash;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        password_hash = buffer.readBytes();
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
        buff.writeBytes(password_hash);
    }


    public int getConstructor() {
        return ID;
    }
}