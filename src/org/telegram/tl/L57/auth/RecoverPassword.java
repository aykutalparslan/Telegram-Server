package org.telegram.tl.L57.auth;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class RecoverPassword extends TLObject {

    public static final int ID = 0x4ea56e92;

    public String code;

    public RecoverPassword() {
    }

    public RecoverPassword(String code) {
        this.code = code;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        code = buffer.readString();
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
        buff.writeString(code);
    }


    public int getConstructor() {
        return ID;
    }
}