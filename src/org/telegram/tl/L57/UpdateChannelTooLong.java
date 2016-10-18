package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateChannelTooLong extends TLUpdate {

    public static final int ID = 0xeb0467fb;

    public int flags;
    public int channel_id;
    public int pts;

    public UpdateChannelTooLong() {
    }

    public UpdateChannelTooLong(int flags, int channel_id, int pts) {
        this.flags = flags;
        this.channel_id = channel_id;
        this.pts = pts;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        channel_id = buffer.readInt();
        if ((flags & (1 << 0)) != 0) {
            pts = buffer.readInt();
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(32);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (pts != 0) {
            flags |= (1 << 0);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeInt(channel_id);
        if ((flags & (1 << 0)) != 0) {
            buff.writeInt(pts);
        }
    }


    public int getConstructor() {
        return ID;
    }
}