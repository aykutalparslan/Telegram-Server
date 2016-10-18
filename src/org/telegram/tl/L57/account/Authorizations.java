package org.telegram.tl.L57.account;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class Authorizations extends TLAuthorizations {

    public static final int ID = 0x1250abde;

    public TLVector<TLAuthorization> authorizations;

    public Authorizations() {
        this.authorizations = new TLVector<>();
    }

    public Authorizations(TLVector<TLAuthorization> authorizations) {
        this.authorizations = authorizations;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        authorizations = (TLVector<TLAuthorization>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(authorizations);
    }


    public int getConstructor() {
        return ID;
    }
}