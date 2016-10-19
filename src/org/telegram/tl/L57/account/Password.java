package org.telegram.tl.L57.account;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class Password extends org.telegram.tl.account.TLPassword {

    public static final int ID = 0x7c18141c;

    public byte[] current_salt;
    public byte[] new_salt;
    public String hint;
    public boolean has_recovery;
    public String email_unconfirmed_pattern;

    public Password() {
    }

    public Password(byte[] current_salt, byte[] new_salt, String hint, boolean has_recovery, String email_unconfirmed_pattern) {
        this.current_salt = current_salt;
        this.new_salt = new_salt;
        this.hint = hint;
        this.has_recovery = has_recovery;
        this.email_unconfirmed_pattern = email_unconfirmed_pattern;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        current_salt = buffer.readBytes();
        new_salt = buffer.readBytes();
        hint = buffer.readString();
        has_recovery = buffer.readBool();
        email_unconfirmed_pattern = buffer.readString();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(69);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeBytes(current_salt);
        buff.writeBytes(new_salt);
        buff.writeString(hint);
        buff.writeBool(has_recovery);
        buff.writeString(email_unconfirmed_pattern);
    }


    public int getConstructor() {
        return ID;
    }
}