package org.telegram.tl.L57.account;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SetPrivacy extends TLObject {

    public static final int ID = 0xc9f81ce8;

    public TLInputPrivacyKey key;
    public TLVector<TLInputPrivacyRule> rules;

    public SetPrivacy() {
        this.rules = new TLVector<>();
    }

    public SetPrivacy(TLInputPrivacyKey key, TLVector<TLInputPrivacyRule> rules) {
        this.key = key;
        this.rules = rules;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        key = (TLInputPrivacyKey) buffer.readTLObject(APIContext.getInstance());
        rules = (TLVector<TLInputPrivacyRule>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(rules);
    }


    public int getConstructor() {
        return ID;
    }
}