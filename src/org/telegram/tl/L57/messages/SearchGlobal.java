package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SearchGlobal extends TLObject {

    public static final int ID = 0x9e3cacb0;

    public String q;
    public int offset_date;
    public TLInputPeer offset_peer;
    public int offset_id;
    public int limit;

    public SearchGlobal() {
    }

    public SearchGlobal(String q, int offset_date, TLInputPeer offset_peer, int offset_id, int limit) {
        this.q = q;
        this.offset_date = offset_date;
        this.offset_peer = offset_peer;
        this.offset_id = offset_id;
        this.limit = limit;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        q = buffer.readString();
        offset_date = buffer.readInt();
        offset_peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        offset_id = buffer.readInt();
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
        buff.writeString(q);
        buff.writeInt(offset_date);
        buff.writeTLObject(offset_peer);
        buff.writeInt(offset_id);
        buff.writeInt(limit);
    }


    public int getConstructor() {
        return ID;
    }
}