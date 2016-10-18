package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateNotifySettings extends TLUpdate {

    public static final int ID = 0xbec268ef;

    public TLNotifyPeer peer;
    public TLPeerNotifySettings notify_settings;

    public UpdateNotifySettings() {
    }

    public UpdateNotifySettings(TLNotifyPeer peer, TLPeerNotifySettings notify_settings) {
        this.peer = peer;
        this.notify_settings = notify_settings;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLNotifyPeer) buffer.readTLObject(APIContext.getInstance());
        notify_settings = (TLPeerNotifySettings) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(notify_settings);
    }


    public int getConstructor() {
        return ID;
    }
}