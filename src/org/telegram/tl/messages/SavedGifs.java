package org.telegram.tl.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class SavedGifs extends TLSavedGifs {

    public static final int ID = 0x2e0709a5;

    public int hash;
    public TLVector<TLDocument> gifs;

    public SavedGifs() {
        this.gifs = new TLVector<>();
    }

    public SavedGifs(int hash, TLVector<TLDocument> gifs) {
        this.hash = hash;
        this.gifs = gifs;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        hash = buffer.readInt();
        gifs = (TLVector<TLDocument>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeInt(hash);
        buff.writeTLObject(gifs);
    }


    public int getConstructor() {
        return ID;
    }
}