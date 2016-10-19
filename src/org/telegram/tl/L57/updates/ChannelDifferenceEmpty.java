package org.telegram.tl.L57.updates;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ChannelDifferenceEmpty extends org.telegram.tl.updates.TLChannelDifference {

    public static final int ID = 0x3e11affb;

    public int flags;
    public int pts;
    public int timeout;

    public ChannelDifferenceEmpty() {
    }

    public ChannelDifferenceEmpty(int flags, int pts, int timeout) {
        this.flags = flags;
        this.pts = pts;
        this.timeout = timeout;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        pts = buffer.readInt();
        if ((flags & (1 << 1)) != 0) {
            timeout = buffer.readInt();
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(28);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (timeout != 0) {
            flags |= (1 << 1);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeInt(pts);
        if ((flags & (1 << 1)) != 0) {
            buff.writeInt(timeout);
        }
    }

    public boolean is_final() {
        return (flags & (1 << 0)) != 0;
    }

    public void set_final(boolean v) {
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