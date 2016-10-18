package org.telegram.tl.L57.auth;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;
import org.telegram.tl.auth.*;

public class SentCode extends TLSentCode {

    public static final int ID = 0x5e002502;

    public int flags;
    public TLSentCodeType type;
    public String phone_code_hash;
    public TLCodeType next_type;
    public int timeout;

    public SentCode() {
    }

    public SentCode(int flags, TLSentCodeType type, String phone_code_hash, TLCodeType next_type, int timeout) {
        this.flags = flags;
        this.type = type;
        this.phone_code_hash = phone_code_hash;
        this.next_type = next_type;
        this.timeout = timeout;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        type = (TLSentCodeType) buffer.readTLObject(APIContext.getInstance());
        phone_code_hash = buffer.readString();
        if ((flags & (1 << 1)) != 0) {
            next_type = (TLCodeType) buffer.readTLObject(APIContext.getInstance());
        }
        if ((flags & (1 << 2)) != 0) {
            timeout = buffer.readInt();
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
        if (next_type != null) {
            flags |= (1 << 1);
        }
        if (timeout != 0) {
            flags |= (1 << 2);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeTLObject(type);
        buff.writeString(phone_code_hash);
        if ((flags & (1 << 1)) != 0) {
            buff.writeTLObject(next_type);
        }
        if ((flags & (1 << 2)) != 0) {
            buff.writeInt(timeout);
        }
    }

    public boolean is_phone_registered() {
        return (flags & (1 << 0)) != 0;
    }

    public void set_phone_registered(boolean v) {
        if (v) {
            flags |= (1 << 0);
        } else {
            flags &= ~(1 << 0);
        }
    }

    public int getConstructor() {
        return ID;
    }
}