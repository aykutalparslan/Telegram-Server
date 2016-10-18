package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InstallStickerSet extends TLObject {

    public static final int ID = 0xc78fe460;

    public TLInputStickerSet stickerset;
    public boolean archived;

    public InstallStickerSet() {
    }

    public InstallStickerSet(TLInputStickerSet stickerset, boolean archived) {
        this.stickerset = stickerset;
        this.archived = archived;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        stickerset = (TLInputStickerSet) buffer.readTLObject(APIContext.getInstance());
        archived = buffer.readBool();
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
        buff.writeTLObject(stickerset);
        buff.writeBool(archived);
    }


    public int getConstructor() {
        return ID;
    }
}