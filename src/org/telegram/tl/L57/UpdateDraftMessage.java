package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateDraftMessage extends TLUpdate {

    public static final int ID = 0xee2bb969;

    public TLPeer peer;
    public TLDraftMessage draft;

    public UpdateDraftMessage() {
    }

    public UpdateDraftMessage(TLPeer peer, TLDraftMessage draft) {
        this.peer = peer;
        this.draft = draft;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLPeer) buffer.readTLObject(APIContext.getInstance());
        draft = (TLDraftMessage) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(peer);
        buff.writeTLObject(draft);
    }


    public int getConstructor() {
        return ID;
    }
}