package org.telegram.tl.L57.contacts;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class GetTopPeers extends TLObject {

    public static final int ID = 0xd4982db5;

    public int flags;
    public int offset;
    public int limit;
    public int hash;

    public GetTopPeers() {
    }

    public GetTopPeers(int flags, int offset, int limit, int hash) {
        this.flags = flags;
        this.offset = offset;
        this.limit = limit;
        this.hash = hash;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        offset = buffer.readInt();
        limit = buffer.readInt();
        hash = buffer.readInt();
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
        buff.writeInt(offset);
        buff.writeInt(limit);
        buff.writeInt(hash);
    }

    public boolean is_correspondents() {
        return (flags & (1 << 0)) != 0;
    }

    public void set_correspondents(boolean v) {
        if (v) {
            flags |= (1 << 0);
        } else {
            flags &= ~(1 << 0);
        }
    }

    public boolean is_bots_pm() {
        return (flags & (1 << 1)) != 0;
    }

    public void set_bots_pm(boolean v) {
        if (v) {
            flags |= (1 << 1);
        } else {
            flags &= ~(1 << 1);
        }
    }

    public boolean is_bots_inline() {
        return (flags & (1 << 2)) != 0;
    }

    public void set_bots_inline(boolean v) {
        if (v) {
            flags |= (1 << 2);
        } else {
            flags &= ~(1 << 2);
        }
    }

    public boolean is_groups() {
        return (flags & (1 << 10)) != 0;
    }

    public void set_groups(boolean v) {
        if (v) {
            flags |= (1 << 10);
        } else {
            flags &= ~(1 << 10);
        }
    }

    public boolean is_channels() {
        return (flags & (1 << 15)) != 0;
    }

    public void set_channels(boolean v) {
        if (v) {
            flags |= (1 << 15);
        } else {
            flags &= ~(1 << 15);
        }
    }

    public int getConstructor() {
        return ID;
    }
}