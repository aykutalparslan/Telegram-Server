package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class GetGameHighScores extends TLObject {

    public static final int ID = 0xe822649d;

    public TLInputPeer peer;
    public int id;
    public TLInputUser user_id;

    public GetGameHighScores() {
    }

    public GetGameHighScores(TLInputPeer peer, int id, TLInputUser user_id) {
        this.peer = peer;
        this.id = id;
        this.user_id = user_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        id = buffer.readInt();
        user_id = (TLInputUser) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeInt(id);
        buff.writeTLObject(user_id);
    }


    public int getConstructor() {
        return ID;
    }
}