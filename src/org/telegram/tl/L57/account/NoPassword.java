package org.telegram.tl.L57.account;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class NoPassword extends org.telegram.tl.account.TLPassword {

    public static final int ID = 0x96dabc18;

    public byte[] new_salt;
    public String email_unconfirmed_pattern;

    public NoPassword() {
    }

    public NoPassword(byte[] new_salt, String email_unconfirmed_pattern) {
        this.new_salt = new_salt;
        this.email_unconfirmed_pattern = email_unconfirmed_pattern;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        new_salt = buffer.readBytes();
        email_unconfirmed_pattern = buffer.readString();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(36);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeBytes(new_salt);
        buff.writeString(email_unconfirmed_pattern);
    }


    public int getConstructor() {
        return ID;
    }
}