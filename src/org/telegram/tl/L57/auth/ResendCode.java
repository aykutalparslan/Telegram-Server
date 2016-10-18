package org.telegram.tl.L57.auth;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ResendCode extends TLObject {

    public static final int ID = 0x3ef1a9bf;

    public String phone_number;
    public String phone_code_hash;

    public ResendCode() {
    }

    public ResendCode(String phone_number, String phone_code_hash) {
        this.phone_number = phone_number;
        this.phone_code_hash = phone_code_hash;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        phone_number = buffer.readString();
        phone_code_hash = buffer.readString();
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
        buff.writeString(phone_number);
        buff.writeString(phone_code_hash);
    }


    public int getConstructor() {
        return ID;
    }
}