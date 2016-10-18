package org.telegram.tl.L57.account;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateNotifySettings extends TLObject {

    public static final int ID = 0x84be5b93;

    public TLInputNotifyPeer peer;
    public TLInputPeerNotifySettings settings;

    public UpdateNotifySettings() {
    }

    public UpdateNotifySettings(TLInputNotifyPeer peer, TLInputPeerNotifySettings settings) {
        this.peer = peer;
        this.settings = settings;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLInputNotifyPeer) buffer.readTLObject(APIContext.getInstance());
        settings = (TLInputPeerNotifySettings) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(settings);
    }


    public int getConstructor() {
        return ID;
    }
}