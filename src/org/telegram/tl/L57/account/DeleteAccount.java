package org.telegram.tl.L57.account;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class DeleteAccount extends TLObject {

    public static final int ID = 0x418d4e0b;

    public String reason;

    public DeleteAccount() {
    }

    public DeleteAccount(String reason) {
        this.reason = reason;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        reason = buffer.readString();
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
        buff.writeString(reason);
    }


    public int getConstructor() {
        return ID;
    }
}