package org.telegram.tl.L57.account;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class CheckUsername extends TLObject {

    public static final int ID = 0x2714d86c;

    public String username;

    public CheckUsername() {
    }

    public CheckUsername(String username) {
        this.username = username;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        username = buffer.readString();
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
        buff.writeString(username);
    }


    public int getConstructor() {
        return ID;
    }
}