package org.telegram.tl.L57.auth;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class BindTempAuthKey extends TLObject {

    public static final int ID = 0xcdd42a05;

    public long perm_auth_key_id;
    public long nonce;
    public int expires_at;
    public byte[] encrypted_message;

    public BindTempAuthKey() {
    }

    public BindTempAuthKey(long perm_auth_key_id, long nonce, int expires_at, byte[] encrypted_message) {
        this.perm_auth_key_id = perm_auth_key_id;
        this.nonce = nonce;
        this.expires_at = expires_at;
        this.encrypted_message = encrypted_message;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        perm_auth_key_id = buffer.readLong();
        nonce = buffer.readLong();
        expires_at = buffer.readInt();
        encrypted_message = buffer.readBytes();
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
        buff.writeLong(perm_auth_key_id);
        buff.writeLong(nonce);
        buff.writeInt(expires_at);
        buff.writeBytes(encrypted_message);
    }


    public int getConstructor() {
        return ID;
    }
}