package org.telegram.tl.L57.contacts;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ResolveUsername extends TLObject {

    public static final int ID = 0xf93ccba3;

    public String username;

    public ResolveUsername() {
    }

    public ResolveUsername(String username) {
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