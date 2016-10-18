package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputBotInlineMessageID extends TLInputBotInlineMessageID {

    public static final int ID = 0x890c3d89;

    public int dc_id;
    public long id;
    public long access_hash;

    public InputBotInlineMessageID() {
    }

    public InputBotInlineMessageID(int dc_id, long id, long access_hash) {
        this.dc_id = dc_id;
        this.id = id;
        this.access_hash = access_hash;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        dc_id = buffer.readInt();
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
        buff.writeInt(dc_id);
        buff.writeLong(id);
        buff.writeLong(access_hash);
    }


    public int getConstructor() {
        return ID;
    }
}