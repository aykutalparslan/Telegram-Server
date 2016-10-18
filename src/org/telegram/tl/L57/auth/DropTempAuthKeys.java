package org.telegram.tl.L57.auth;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class DropTempAuthKeys extends TLObject {

    public static final int ID = 0x8e48a188;

    public TLVector<Long> except_auth_keys;

    public DropTempAuthKeys() {
        this.except_auth_keys = new TLVector<>();
    }

    public DropTempAuthKeys(TLVector<Long> except_auth_keys) {
        this.except_auth_keys = except_auth_keys;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        except_auth_keys = (TLVector<Long>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(except_auth_keys);
    }


    public int getConstructor() {
        return ID;
    }
}