package org.telegram.tl.L57.auth;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SendInvites extends TLObject {

    public static final int ID = 0x771c1d97;

    public TLVector<String> phone_numbers;
    public String message;

    public SendInvites() {
        this.phone_numbers = new TLVector<>();
    }

    public SendInvites(TLVector<String> phone_numbers, String message) {
        this.phone_numbers = phone_numbers;
        this.message = message;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        phone_numbers = (TLVector<String>) buffer.readTLObject(APIContext.getInstance());
        message = buffer.readString();
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
        buff.writeTLObject(phone_numbers);
        buff.writeString(message);
    }


    public int getConstructor() {
        return ID;
    }
}