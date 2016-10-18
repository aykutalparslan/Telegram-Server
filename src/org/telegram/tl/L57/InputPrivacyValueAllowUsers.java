package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputPrivacyValueAllowUsers extends TLInputPrivacyRule {

    public static final int ID = 0x131cc67f;

    public TLVector<TLInputUser> users;

    public InputPrivacyValueAllowUsers() {
        this.users = new TLVector<>();
    }

    public InputPrivacyValueAllowUsers(TLVector<TLInputUser> users) {
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        users = (TLVector<TLInputUser>) buffer.readTLObject(APIContext.getInstance());
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