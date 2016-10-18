package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateReadHistoryInbox extends TLUpdate {

    public static final int ID = 0x9961fd5c;

    public TLPeer peer;
    public int max_id;
    public int pts;
    public int pts_count;

    public UpdateReadHistoryInbox() {
    }

    public UpdateReadHistoryInbox(TLPeer peer, int max_id, int pts, int pts_count) {
        this.peer = peer;
        this.max_id = max_id;
        this.pts = pts;
        this.pts_count = pts_count;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLPeer) buffer.readTLObject(APIContext.getInstance());
        max_id = buffer.readInt();
        pts = buffer.readInt();
        pts_count = buffer.readInt();
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
        buff.writeInt(max_id);
        buff.writeInt(pts);
        buff.writeInt(pts_count);
    }


    public int getConstructor() {
        return ID;
    }
}