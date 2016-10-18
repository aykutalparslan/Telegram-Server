package org.telegram.tl.L57.account;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class PasswordInputSettings extends TLPasswordInputSettings {

    public static final int ID = 0x86916deb;

    public int flags;
    public byte[] new_salt;
    public byte[] new_password_hash;
    public String hint;
    public String email;

    public PasswordInputSettings() {
    }

    public PasswordInputSettings(int flags, byte[] new_salt, byte[] new_password_hash, String hint, String email) {
        this.flags = flags;
        this.new_salt = new_salt;
        this.new_password_hash = new_password_hash;
        this.hint = hint;
        this.email = email;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        if ((flags & (1 << 0)) != 0) {
            new_salt = buffer.readBytes();
        }
        if ((flags & (1 << 0)) != 0) {
            new_password_hash = buffer.readBytes();
        }
        if ((flags & (1 << 0)) != 0) {
            hint = buffer.readString();
        }
        if ((flags & (1 << 1)) != 0) {
            email = buffer.readString();
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(32);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (new_salt.length != 0) {
            flags |= (1 << 0);
        }
        if (new_password_hash.length != 0) {
            flags |= (1 << 0);
        }
        if (hint != null && !hint.isEmpty()) {
            flags |= (1 << 0);
        }
        if (email != null && !email.isEmpty()) {
            flags |= (1 << 1);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        if ((flags & (1 << 0)) != 0) {
            buff.writeBytes(new_salt);
        }
        if ((flags & (1 << 0)) != 0) {
            buff.writeBytes(new_password_hash);
        }
        if ((flags & (1 << 0)) != 0) {
            buff.writeString(hint);
        }
        if ((flags & (1 << 1)) != 0) {
            buff.writeString(email);
        }
    }


    public int getConstructor() {
        return ID;
    }
}