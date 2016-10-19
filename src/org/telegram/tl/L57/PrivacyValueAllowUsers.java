package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class PrivacyValueAllowUsers extends org.telegram.tl.TLPrivacyRule {

    public static final int ID = 0x4d5bbe0c;

    public TLVector<Integer> users;

    public PrivacyValueAllowUsers() {
        this.users = new TLVector<>();
    }

    public PrivacyValueAllowUsers(TLVector<Integer> users) {
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        users = (TLVector<Integer>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(12);
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