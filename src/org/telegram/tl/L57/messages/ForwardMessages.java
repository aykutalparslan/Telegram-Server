package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ForwardMessages extends TLObject {

    public static final int ID = 0x708e0195;

    public int flags;
    public TLInputPeer from_peer;
    public TLVector<Integer> id;
    public TLVector<Long> random_id;
    public TLInputPeer to_peer;

    public ForwardMessages() {
        this.id = new TLVector<>();
        this.random_id = new TLVector<>();
    }

    public ForwardMessages(int flags, TLInputPeer from_peer, TLVector<Integer> id, TLVector<Long> random_id, TLInputPeer to_peer) {
        this.flags = flags;
        this.from_peer = from_peer;
        this.id = id;
        this.random_id = random_id;
        this.to_peer = to_peer;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        from_peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        id = (TLVector<Integer>) buffer.readTLObject(APIContext.getInstance());
        random_id = (TLVector<Long>) buffer.readTLObject(APIContext.getInstance());
        to_peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(32);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeTLObject(from_peer);
        buff.writeTLObject(id);
        buff.writeTLObject(random_id);
        buff.writeTLObject(to_peer);
    }

    public boolean is_silent() {
        return (flags & (1 << 5)) != 0;
    }

    public void set_silent(boolean v) {
        if (v) {
            flags |= (1 << 5);
        } else {
            flags &= ~(1 << 5);
        }
    }

    public boolean is_background() {
        return (flags & (1 << 6)) != 0;
    }

    public void set_background(boolean v) {
        if (v) {
            flags |= (1 << 6);
        } else {
            flags &= ~(1 << 6);
        }
    }

    public boolean is_with_my_score() {
        return (flags & (1 << 8)) != 0;
    }

    public void set_with_my_score(boolean v) {
        if (v) {
            flags |= (1 << 8);
        } else {
            flags &= ~(1 << 8);
        }
    }

    public int getConstructor() {
        return ID;
    }
}