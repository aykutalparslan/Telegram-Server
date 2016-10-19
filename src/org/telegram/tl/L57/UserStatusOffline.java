package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UserStatusOffline extends org.telegram.tl.TLUserStatus {

    public static final int ID = 0x8c703f;

    public int was_online;

    public UserStatusOffline() {
    }

    public UserStatusOffline(int was_online) {
        this.was_online = was_online;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        was_online = buffer.readInt();
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
        buff.writeInt(was_online);
    }


    public int getConstructor() {
        return ID;
    }
}