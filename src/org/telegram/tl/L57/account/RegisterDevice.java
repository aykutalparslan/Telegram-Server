package org.telegram.tl.L57.account;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class RegisterDevice extends TLObject {

    public static final int ID = 0x637ea878;

    public int token_type;
    public String token;

    public RegisterDevice() {
    }

    public RegisterDevice(int token_type, String token) {
        this.token_type = token_type;
        this.token = token;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        token_type = buffer.readInt();
        token = buffer.readString();
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
        buff.writeInt(token_type);
        buff.writeString(token);
    }


    public int getConstructor() {
        return ID;
    }
}