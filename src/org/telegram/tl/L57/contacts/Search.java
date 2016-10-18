package org.telegram.tl.L57.contacts;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class Search extends TLObject {

    public static final int ID = 0x11f812d8;

    public String q;
    public int limit;

    public Search() {
    }

    public Search(String q, int limit) {
        this.q = q;
        this.limit = limit;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        q = buffer.readString();
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
        buff.writeInt(limit);
    }


    public int getConstructor() {
        return ID;
    }
}