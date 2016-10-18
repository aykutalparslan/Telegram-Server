package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class Search extends TLObject {

    public static final int ID = 0xd4569248;

    public int flags;
    public TLInputPeer peer;
    public String q;
    public TLMessagesFilter filter;
    public int min_date;
    public int max_date;
    public int offset;
    public int max_id;
    public int limit;

    public Search() {
    }

    public Search(int flags, TLInputPeer peer, String q, TLMessagesFilter filter, int min_date, int max_date, int offset, int max_id, int limit) {
        this.flags = flags;
        this.peer = peer;
        this.q = q;
        this.filter = filter;
        this.min_date = min_date;
        this.max_date = max_date;
        this.offset = offset;
        this.max_id = max_id;
        this.limit = limit;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        q = buffer.readString();
        filter = (TLMessagesFilter) buffer.readTLObject(APIContext.getInstance());
        min_date = buffer.readInt();
        max_date = buffer.readInt();
        offset = buffer.readInt();
        max_id = buffer.readInt();
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
        buff.writeInt(flags);
        buff.writeTLObject(peer);
        buff.writeString(q);
        buff.writeTLObject(filter);
        buff.writeInt(min_date);
        buff.writeInt(max_date);
        buff.writeInt(offset);
        buff.writeInt(max_id);
        buff.writeInt(limit);
    }


    public int getConstructor() {
        return ID;
    }
}