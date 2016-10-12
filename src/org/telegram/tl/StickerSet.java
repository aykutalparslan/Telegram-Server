package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class StickerSet extends TLStickerSet {

    public static final int ID = 0xcd303b41;

    public int flags;
    public long id;
    public long access_hash;
    public String title;
    public String short_name;
    public int count;
    public int hash;

    public StickerSet() {
    }

    public StickerSet(int flags, long id, long access_hash, String title, String short_name, int count, int hash) {
        this.flags = flags;
        this.id = id;
        this.access_hash = access_hash;
        this.title = title;
        this.short_name = short_name;
        this.count = count;
        this.hash = hash;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = buffer.readLong();
        access_hash = buffer.readLong();
        title = buffer.readString();
        short_name = buffer.readString();
        count = buffer.readInt();
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
        buff.writeLong(id);
        buff.writeLong(access_hash);
        buff.writeString(title);
        buff.writeString(short_name);
        buff.writeInt(count);
        buff.writeInt(hash);
    }

    public boolean is_stickerSet_installed() {
        return (flags & (1 << 0)) != 0;
    }

    public boolean set_stickerSet_installed() {
        return (flags |= (1 << 0)) != 0;
    }

    public boolean is_stickerSet_archived() {
        return (flags & (1 << 1)) != 0;
    }

    public boolean set_stickerSet_archived() {
        return (flags |= (1 << 1)) != 0;
    }

    public boolean is_stickerSet_official() {
        return (flags & (1 << 2)) != 0;
    }

    public boolean set_stickerSet_official() {
        return (flags |= (1 << 2)) != 0;
    }

    public boolean is_stickerSet_masks() {
        return (flags & (1 << 3)) != 0;
    }

    public boolean set_stickerSet_masks() {
        return (flags |= (1 << 3)) != 0;
    }

    public int getConstructor() {
        return ID;
    }
}