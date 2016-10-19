package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class FileLocation extends org.telegram.tl.TLFileLocation {

    public static final int ID = 0x53d69076;

    public int dc_id;
    public long volume_id;
    public int local_id;
    public long secret;

    public FileLocation() {
    }

    public FileLocation(int dc_id, long volume_id, int local_id, long secret) {
        this.dc_id = dc_id;
        this.volume_id = volume_id;
        this.local_id = local_id;
        this.secret = secret;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        dc_id = buffer.readInt();
        volume_id = buffer.readLong();
        local_id = buffer.readInt();
        secret = buffer.readLong();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(28);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(dc_id);
        buff.writeLong(volume_id);
        buff.writeInt(local_id);
        buff.writeLong(secret);
    }


    public int getConstructor() {
        return ID;
    }
}