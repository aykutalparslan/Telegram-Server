package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class GetArchivedStickers extends TLObject {

    public static final int ID = 0x57f17692;

    public int flags;
    public long offset_id;
    public int limit;

    public GetArchivedStickers() {
    }

    public GetArchivedStickers(int flags, long offset_id, int limit) {
        this.flags = flags;
        this.offset_id = offset_id;
        this.limit = limit;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        offset_id = buffer.readLong();
        limit = buffer.readInt();
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
        buff.writeLong(offset_id);
        buff.writeInt(limit);
    }

    public boolean is_masks() {
        return (flags & (1 << 0)) != 0;
    }

    public void set_masks(boolean v) {
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