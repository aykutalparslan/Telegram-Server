package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UserStatusOnline extends org.telegram.tl.TLUserStatus {

    public static final int ID = 0xedb93949;

    public int expires;

    public UserStatusOnline() {
    }

    public UserStatusOnline(int expires) {
        this.expires = expires;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        expires = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(8);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(expires);
    }


    public int getConstructor() {
        return ID;
    }
}