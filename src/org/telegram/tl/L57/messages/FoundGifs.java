package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class FoundGifs extends TLFoundGifs {

    public static final int ID = 0x450a1c0a;

    public int next_offset;
    public TLVector<TLFoundGif> results;

    public FoundGifs() {
        this.results = new TLVector<>();
    }

    public FoundGifs(int next_offset, TLVector<TLFoundGif> results) {
        this.next_offset = next_offset;
        this.results = results;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        next_offset = buffer.readInt();
        results = (TLVector<TLFoundGif>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeInt(next_offset);
        buff.writeTLObject(results);
    }


    public int getConstructor() {
        return ID;
    }
}