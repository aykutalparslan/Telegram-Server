package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class PrivacyValueDisallowUsers extends TLPrivacyRule {

    public static final int ID = 0xc7f49b7;

    public TLVector<Integer> users;

    public PrivacyValueDisallowUsers() {
        this.users = new TLVector<>();
    }

    public PrivacyValueDisallowUsers(TLVector<Integer> users) {
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        users = (TLVector<Integer>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(users);
    }


    public int getConstructor() {
        return ID;
    }
}