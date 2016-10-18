package org.telegram.tl.L57.account;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ConfirmPhone extends TLObject {

    public static final int ID = 0x5f2178c3;

    public String phone_code_hash;
    public String phone_code;

    public ConfirmPhone() {
    }

    public ConfirmPhone(String phone_code_hash, String phone_code) {
        this.phone_code_hash = phone_code_hash;
        this.phone_code = phone_code;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        phone_code_hash = buffer.readString();
        phone_code = buffer.readString();
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
        buff.writeString(phone_code_hash);
        buff.writeString(phone_code);
    }


    public int getConstructor() {
        return ID;
    }
}