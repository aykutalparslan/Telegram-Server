package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SaveGif extends TLObject {

    public static final int ID = 0x327a30cb;

    public TLInputDocument id;
    public boolean unsave;

    public SaveGif() {
    }

    public SaveGif(TLInputDocument id, boolean unsave) {
        this.id = id;
        this.unsave = unsave;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = (TLInputDocument) buffer.readTLObject(APIContext.getInstance());
        unsave = buffer.readBool();
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
        buff.writeTLObject(id);
        buff.writeBool(unsave);
    }


    public int getConstructor() {
        return ID;
    }
}