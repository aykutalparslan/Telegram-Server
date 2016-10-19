package org.telegram.tl.L57.account;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class PrivacyRules extends org.telegram.tl.account.TLPrivacyRules {

    public static final int ID = 0x554abb6f;

    public TLVector<org.telegram.tl.TLPrivacyRule> rules;
    public TLVector<org.telegram.tl.TLUser> users;

    public PrivacyRules() {
        this.rules = new TLVector<>();
        this.users = new TLVector<>();
    }

    public PrivacyRules(TLVector<org.telegram.tl.TLPrivacyRule> rules, TLVector<org.telegram.tl.TLUser> users) {
        this.rules = rules;
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        rules = (TLVector<org.telegram.tl.TLPrivacyRule>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(rules);
        buff.writeTLObject(users);
    }


    public int getConstructor() {
        return ID;
    }
}