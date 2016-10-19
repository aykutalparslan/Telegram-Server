package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdatePrivacy extends org.telegram.tl.TLUpdate {

    public static final int ID = 0xee3b272a;

    public org.telegram.tl.TLPrivacyKey key;
    public TLVector<org.telegram.tl.TLPrivacyRule> rules;

    public UpdatePrivacy() {
        this.rules = new TLVector<>();
    }

    public UpdatePrivacy(org.telegram.tl.TLPrivacyKey key, TLVector<org.telegram.tl.TLPrivacyRule> rules) {
        this.key = key;
        this.rules = rules;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        key = (org.telegram.tl.TLPrivacyKey) buffer.readTLObject(APIContext.getInstance());
        rules = (TLVector<org.telegram.tl.TLPrivacyRule>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(key);
        buff.writeTLObject(rules);
    }


    public int getConstructor() {
        return ID;
    }
}