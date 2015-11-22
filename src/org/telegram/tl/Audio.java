package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class Audio extends TLAudio {

    public static final int ID = 0xf9e35055;

    public long id;
    public long access_hash;
    public int date;
    public int duration;
    public String mime_type;
    public int size;
    public int dc_id;

    public Audio() {
    }

    public Audio(long id, long access_hash, int date, int duration, String mime_type, int size, int dc_id) {
        this.id = id;
        this.access_hash = access_hash;
        this.date = date;
        this.duration = duration;
        this.mime_type = mime_type;
        this.size = size;
        this.dc_id = dc_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readLong();
        access_hash = buffer.readLong();
        date = buffer.readInt();
        duration = buffer.readInt();
        mime_type = buffer.readString();
        size = buffer.readInt();
        dc_id = buffer.readInt();
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
        buff.writeLong(id);
        buff.writeLong(access_hash);
        buff.writeInt(date);
        buff.writeInt(duration);
        buff.writeString(mime_type);
        buff.writeInt(size);
        buff.writeInt(dc_id);
    }

    public int getConstructor() {
        return ID;
    }
}