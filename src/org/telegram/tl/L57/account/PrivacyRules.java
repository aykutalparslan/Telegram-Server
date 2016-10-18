package org.telegram.tl.L57.account;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class PrivacyRules extends TLPrivacyRules {

    public static final int ID = 0x554abb6f;

    public TLVector<TLPrivacyRule> rules;
    public TLVector<TLUser> users;

    public PrivacyRules() {
        this.rules = new TLVector<>();
        this.users = new TLVector<>();
    }

    public PrivacyRules(TLVector<TLPrivacyRule> rules, TLVector<TLUser> users) {
        this.rules = rules;
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        rules = (TLVector<TLPrivacyRule>) buffer.readTLObject(APIContext.getInstance());
        users = (TLVector<TLUser>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(rules);
        buff.writeTLObject(users);
    }


    public int getConstructor() {
        return ID;
    }
}