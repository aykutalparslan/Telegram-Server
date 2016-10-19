package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SavedGifs extends org.telegram.tl.messages.TLSavedGifs {

    public static final int ID = 0x2e0709a5;

    public int hash;
    public TLVector<org.telegram.tl.TLDocument> gifs;

    public SavedGifs() {
        this.gifs = new TLVector<>();
    }

    public SavedGifs(int hash, TLVector<org.telegram.tl.TLDocument> gifs) {
        this.hash = hash;
        this.gifs = gifs;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        hash = buffer.readInt();
        gifs = (TLVector<org.telegram.tl.TLDocument>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(16);
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