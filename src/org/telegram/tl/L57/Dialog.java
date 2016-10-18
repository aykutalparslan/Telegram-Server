package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class Dialog extends TLDialog {

    public static final int ID = 0x66ffba14;

    public int flags;
    public TLPeer peer;
    public int top_message;
    public int read_inbox_max_id;
    public int read_outbox_max_id;
    public int unread_count;
    public TLPeerNotifySettings notify_settings;
    public int pts;
    public TLDraftMessage draft;

    public Dialog() {
    }

    public Dialog(int flags, TLPeer peer, int top_message, int read_inbox_max_id, int read_outbox_max_id, int unread_count, TLPeerNotifySettings notify_settings, int pts, TLDraftMessage draft) {
        this.flags = flags;
        this.peer = peer;
        this.top_message = top_message;
        this.read_inbox_max_id = read_inbox_max_id;
        this.read_outbox_max_id = read_outbox_max_id;
        this.unread_count = unread_count;
        this.notify_settings = notify_settings;
        this.pts = pts;
        this.draft = draft;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        peer = (TLPeer) buffer.readTLObject(APIContext.getInstance());
        top_message = buffer.readInt();
        read_inbox_max_id = buffer.readInt();
        read_outbox_max_id = buffer.readInt();
        unread_count = buffer.readInt();
        notify_settings = (TLPeerNotifySettings) buffer.readTLObject(APIContext.getInstance());
        if ((flags & (1 << 0)) != 0) {
            pts = buffer.readInt();
        }
        if ((flags & (1 << 1)) != 0) {
            draft = (TLDraftMessage) buffer.readTLObject(APIContext.getInstance());
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(32);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (pts != 0) {
            flags |= (1 << 0);
        }
        if (draft != null) {
            flags |= (1 << 1);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeTLObject(peer);
        buff.writeInt(top_message);
        buff.writeInt(read_inbox_max_id);
        buff.writeInt(read_outbox_max_id);
        buff.writeInt(unread_count);
        buff.writeTLObject(notify_settings);
        if ((flags & (1 << 0)) != 0) {
            buff.writeInt(pts);
        }
        if ((flags & (1 << 1)) != 0) {
            buff.writeTLObject(draft);
        }
    }


    public int getConstructor() {
        return ID;
    }
}