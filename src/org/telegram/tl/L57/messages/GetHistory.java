package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class GetHistory extends TLObject {

    public static final int ID = 0xafa92846;

    public TLInputPeer peer;
    public int offset_id;
    public int offset_date;
    public int add_offset;
    public int limit;
    public int max_id;
    public int min_id;

    public GetHistory() {
    }

    public GetHistory(TLInputPeer peer, int offset_id, int offset_date, int add_offset, int limit, int max_id, int min_id) {
        this.peer = peer;
        this.offset_id = offset_id;
        this.offset_date = offset_date;
        this.add_offset = add_offset;
        this.limit = limit;
        this.max_id = max_id;
        this.min_id = min_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        offset_id = buffer.readInt();
        offset_date = buffer.readInt();
        add_offset = buffer.readInt();
        limit = buffer.readInt();
        max_id = buffer.readInt();
        min_id = buffer.readInt();
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
        buff.writeTLObject(peer);
        buff.writeInt(offset_id);
        buff.writeInt(offset_date);
        buff.writeInt(add_offset);
        buff.writeInt(limit);
        buff.writeInt(max_id);
        buff.writeInt(min_id);
    }


    public int getConstructor() {
        return ID;
    }
}