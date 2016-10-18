package org.telegram.tl.L57.auth;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SignUp extends TLObject {

    public static final int ID = 0x1b067634;

    public String phone_number;
    public String phone_code_hash;
    public String phone_code;
    public String first_name;
    public String last_name;

    public SignUp() {
    }

    public SignUp(String phone_number, String phone_code_hash, String phone_code, String first_name, String last_name) {
        this.phone_number = phone_number;
        this.phone_code_hash = phone_code_hash;
        this.phone_code = phone_code;
        this.first_name = first_name;
        this.last_name = last_name;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        phone_number = buffer.readString();
        phone_code_hash = buffer.readString();
        phone_code = buffer.readString();
        first_name = buffer.readString();
        last_name = buffer.readString();
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
        buff.writeString(phone_code);
        buff.writeString(first_name);
        buff.writeString(last_name);
    }


    public int getConstructor() {
        return ID;
    }
}