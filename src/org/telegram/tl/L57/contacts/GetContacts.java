package org.telegram.tl.L57.contacts;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class GetContacts extends TLObject {

    public static final int ID = 0x22c6aa08;

    public String hash;

    public GetContacts() {
    }

    public GetContacts(String hash) {
        this.hash = hash;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        hash = buffer.readString();
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
        buff.writeString(hash);
    }


    public int getConstructor() {
        return ID;
    }
}