package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SearchGifs extends TLObject {

    public static final int ID = 0xbf9a776b;

    public String q;
    public int offset;

    public SearchGifs() {
    }

    public SearchGifs(String q, int offset) {
        this.q = q;
        this.offset = offset;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        q = buffer.readString();
        offset = buffer.readInt();
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
        buff.writeInt(offset);
    }


    public int getConstructor() {
        return ID;
    }
}