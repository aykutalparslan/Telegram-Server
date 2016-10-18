package org.telegram.tl.L57.channels;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ToggleInvites extends TLObject {

    public static final int ID = 0x49609307;

    public TLInputChannel channel;
    public boolean enabled;

    public ToggleInvites() {
    }

    public ToggleInvites(TLInputChannel channel, boolean enabled) {
        this.channel = channel;
        this.enabled = enabled;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        channel = (TLInputChannel) buffer.readTLObject(APIContext.getInstance());
        enabled = buffer.readBool();
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
        buff.writeTLObject(channel);
        buff.writeBool(enabled);
    }


    public int getConstructor() {
        return ID;
    }
}