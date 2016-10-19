package org.telegram.tl.L57.contacts;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class Blocked extends org.telegram.tl.contacts.TLBlocked {

    public static final int ID = 0x1c138d15;

    public TLVector<org.telegram.tl.TLContactBlocked> blocked;
    public TLVector<org.telegram.tl.TLUser> users;

    public Blocked() {
        this.blocked = new TLVector<>();
        this.users = new TLVector<>();
    }

    public Blocked(TLVector<org.telegram.tl.TLContactBlocked> blocked, TLVector<org.telegram.tl.TLUser> users) {
        this.blocked = blocked;
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        blocked = (TLVector<org.telegram.tl.TLContactBlocked>) buffer.readTLObject(APIContext.getInstance());
        users = (TLVector<org.telegram.tl.TLUser>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(20);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeTLObject(blocked);
        buff.writeTLObject(users);
    }


    public int getConstructor() {
        return ID;
    }
}