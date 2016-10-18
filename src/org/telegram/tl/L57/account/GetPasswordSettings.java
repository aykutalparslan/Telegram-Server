package org.telegram.tl.L57.account;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class GetPasswordSettings extends TLObject {

    public static final int ID = 0xbc8d11bb;

    public byte[] current_password_hash;

    public GetPasswordSettings() {
    }

    public GetPasswordSettings(byte[] current_password_hash) {
        this.current_password_hash = current_password_hash;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        current_password_hash = buffer.readBytes();
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
        buff.writeBytes(current_password_hash);
    }


    public int getConstructor() {
        return ID;
    }
}