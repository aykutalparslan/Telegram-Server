package org.telegram.tl.L57.channels;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdatePinnedMessage extends TLObject {

    public static final int ID = 0xa72ded52;

    public int flags;
    public TLInputChannel channel;
    public int id;

    public UpdatePinnedMessage() {
    }

    public UpdatePinnedMessage(int flags, TLInputChannel channel, int id) {
        this.flags = flags;
        this.channel = channel;
        this.id = id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        channel = (TLInputChannel) buffer.readTLObject(APIContext.getInstance());
        id = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(32);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeTLObject(channel);
        buff.writeInt(id);
    }

    public boolean is_silent() {
        return (flags & (1 << 0)) != 0;
    }

    public void set_silent(boolean v) {
        if (v) {
            flags |= (1 << 0);
        } else {
            flags &= ~(1 << 0);
        }
    }

    public int getConstructor() {
        return ID;
    }
}