package org.telegram.tl.L57.help;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class Support extends TLSupport {

    public static final int ID = 0x17c6b5f6;

    public String phone_number;
    public TLUser user;

    public Support() {
    }

    public Support(String phone_number, TLUser user) {
        this.phone_number = phone_number;
        this.user = user;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        phone_number = buffer.readString();
        user = (TLUser) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(user);
    }


    public int getConstructor() {
        return ID;
    }
}