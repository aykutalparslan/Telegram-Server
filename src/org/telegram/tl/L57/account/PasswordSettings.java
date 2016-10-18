package org.telegram.tl.L57.account;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class PasswordSettings extends TLPasswordSettings {

    public static final int ID = 0xb7b72ab3;

    public String email;

    public PasswordSettings() {
    }

    public PasswordSettings(String email) {
        this.email = email;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        email = buffer.readString();
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
        buff.writeString(email);
    }


    public int getConstructor() {
        return ID;
    }
}