package org.telegram.tl.L57.account;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class GetPrivacy extends TLObject {

    public static final int ID = 0xdadbc950;

    public TLInputPrivacyKey key;

    public GetPrivacy() {
    }

    public GetPrivacy(TLInputPrivacyKey key) {
        this.key = key;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        key = (TLInputPrivacyKey) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(key);
    }


    public int getConstructor() {
        return ID;
    }
}