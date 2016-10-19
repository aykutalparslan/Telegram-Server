package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputDocumentFileLocation extends org.telegram.tl.TLInputFileLocation {

    public static final int ID = 0x430f0724;

    public long id;
    public long access_hash;
    public int version;

    public InputDocumentFileLocation() {
    }

    public InputDocumentFileLocation(long id, long access_hash, int version) {
        this.id = id;
        this.access_hash = access_hash;
        this.version = version;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readLong();
        access_hash = buffer.readLong();
        version = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(24);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeLong(id);
        buff.writeLong(access_hash);
        buff.writeInt(version);
    }


    public int getConstructor() {
        return ID;
    }
}