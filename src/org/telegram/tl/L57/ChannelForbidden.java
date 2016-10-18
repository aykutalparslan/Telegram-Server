package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ChannelForbidden extends TLChat {

    public static final int ID = 0x8537784f;

    public int flags;
    public int id;
    public long access_hash;
    public String title;

    public ChannelForbidden() {
    }

    public ChannelForbidden(int flags, int id, long access_hash, String title) {
        this.flags = flags;
        this.id = id;
        this.access_hash = access_hash;
        this.title = title;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = buffer.readInt();
        access_hash = buffer.readLong();
        title = buffer.readString();
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
        buff.writeInt(id);
        buff.writeLong(access_hash);
        buff.writeString(title);
    }

    public boolean is_broadcast() {
        return (flags & (1 << 5)) != 0;
    }

    public void set_broadcast(boolean v) {
        if (v) {
            flags |= (1 << 5);
        } else {
            flags &= ~(1 << 5);
        }
    }

    public boolean is_megagroup() {
        return (flags & (1 << 8)) != 0;
    }

    public void set_megagroup(boolean v) {
        if (v) {
            flags |= (1 << 8);
        } else {
            flags &= ~(1 << 8);
        }
    }

    public int getConstructor() {
        return ID;
    }
}