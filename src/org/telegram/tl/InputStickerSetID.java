package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class InputStickerSetID extends TLInputStickerSet {

    public static final int ID = 0x9de7a269;

    public long id;
    public long access_hash;

    public InputStickerSetID() {
    }

    public InputStickerSetID(long id, long access_hash) {
        this.id = id;
        this.access_hash = access_hash;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readLong();
        access_hash = buffer.readLong();
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
    }


    public int getConstructor() {
        return ID;
    }
}