package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class MessageFwdHeader extends TLMessageFwdHeader {

    public static final int ID = 0xc786ddcb;

    public int flags;
    public int from_id;
    public int date;
    public int channel_id;
    public int channel_post;

    public MessageFwdHeader() {
    }

    public MessageFwdHeader(int flags, int from_id, int date, int channel_id, int channel_post) {
        this.flags = flags;
        this.from_id = from_id;
        this.date = date;
        this.channel_id = channel_id;
        this.channel_post = channel_post;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        if ((flags & (1 << 0)) != 0) {
            from_id = buffer.readInt();
        }
        date = buffer.readInt();
        if ((flags & (1 << 1)) != 0) {
            channel_id = buffer.readInt();
        }
        if ((flags & (1 << 2)) != 0) {
            channel_post = buffer.readInt();
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
        if (from_id != 0) {
            flags |= (1 << 0);
        }
        if (channel_id != 0) {
            flags |= (1 << 1);
        }
        if (channel_post != 0) {
            flags |= (1 << 2);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        if ((flags & (1 << 0)) != 0) {
            buff.writeInt(from_id);
        }
        buff.writeInt(date);
        if ((flags & (1 << 1)) != 0) {
            buff.writeInt(channel_id);
        }
        if ((flags & (1 << 2)) != 0) {
            buff.writeInt(channel_post);
        }
    }


    public int getConstructor() {
        return ID;
    }
}