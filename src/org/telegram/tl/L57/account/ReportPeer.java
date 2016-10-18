package org.telegram.tl.L57.account;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ReportPeer extends TLObject {

    public static final int ID = 0xae189d5f;

    public TLInputPeer peer;
    public TLReportReason reason;

    public ReportPeer() {
    }

    public ReportPeer(TLInputPeer peer, TLReportReason reason) {
        this.peer = peer;
        this.reason = reason;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        reason = (TLReportReason) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(reason);
    }


    public int getConstructor() {
        return ID;
    }
}