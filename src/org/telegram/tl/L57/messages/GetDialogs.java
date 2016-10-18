package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class GetDialogs extends TLObject {

    public static final int ID = 0x6b47f94d;

    public int offset_date;
    public int offset_id;
    public TLInputPeer offset_peer;
    public int limit;

    public GetDialogs() {
    }

    public GetDialogs(int offset_date, int offset_id, TLInputPeer offset_peer, int limit) {
        this.offset_date = offset_date;
        this.offset_id = offset_id;
        this.offset_peer = offset_peer;
        this.limit = limit;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        offset_date = buffer.readInt();
        offset_id = buffer.readInt();
        offset_peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        limit = buffer.readInt();
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
        buff.writeInt(offset_date);
        buff.writeInt(offset_id);
        buff.writeTLObject(offset_peer);
        buff.writeInt(limit);
    }


    public int getConstructor() {
        return ID;
    }
}