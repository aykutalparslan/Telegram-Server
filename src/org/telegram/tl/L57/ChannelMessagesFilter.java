package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ChannelMessagesFilter extends org.telegram.tl.TLChannelMessagesFilter {

    public static final int ID = 0xcd77d957;

    public int flags;
    public TLVector<org.telegram.tl.TLMessageRange> ranges;

    public ChannelMessagesFilter() {
        this.ranges = new TLVector<>();
    }

    public ChannelMessagesFilter(int flags, TLVector<org.telegram.tl.TLMessageRange> ranges) {
        this.flags = flags;
        this.ranges = ranges;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        ranges = (TLVector<org.telegram.tl.TLMessageRange>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(24);
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
        buff.writeTLObject(ranges);
    }

    public boolean is_exclude_new_messages() {
        return (flags & (1 << 1)) != 0;
    }

    public void set_exclude_new_messages(boolean v) {
        if (v) {
            flags |= (1 << 1);
        } else {
            flags &= ~(1 << 1);
        }
    }

    public int getConstructor() {
        return ID;
    }
}