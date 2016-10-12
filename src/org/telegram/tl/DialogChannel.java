package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class DialogChannel extends TLDialog {

    public static final int ID = 0x5b8496b2;

    public TLPeer peer;
    public int top_message;
    public int top_important_message;
    public int read_inbox_max_id;
    public int unread_count;
    public int unread_important_count;
    public TLPeerNotifySettings notify_settings;
    public int pts;

    public DialogChannel() {
    }

    public DialogChannel(TLPeer peer, int top_message, int top_important_message, int read_inbox_max_id, int unread_count, int unread_important_count, TLPeerNotifySettings notify_settings, int pts) {
        this.peer = peer;
        this.top_message = top_message;
        this.top_important_message = top_important_message;
        this.read_inbox_max_id = read_inbox_max_id;
        this.unread_count = unread_count;
        this.unread_important_count = unread_important_count;
        this.notify_settings = notify_settings;
        this.pts = pts;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLPeer) buffer.readTLObject(APIContext.getInstance());
        top_message = buffer.readInt();
        top_important_message = buffer.readInt();
        read_inbox_max_id = buffer.readInt();
        unread_count = buffer.readInt();
        unread_important_count = buffer.readInt();
        notify_settings = (TLPeerNotifySettings) buffer.readTLObject(APIContext.getInstance());
        pts = buffer.readInt();
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
        buff.writeInt(top_message);
        buff.writeInt(top_important_message);
        buff.writeInt(read_inbox_max_id);
        buff.writeInt(unread_count);
        buff.writeInt(unread_important_count);
        buff.writeTLObject(notify_settings);
        buff.writeInt(pts);
    }


    public int getConstructor() {
        return ID;
    }
}