package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class GetBotCallbackAnswer extends TLObject {

    public static final int ID = 0x810a9fec;

    public int flags;
    public TLInputPeer peer;
    public int msg_id;
    public byte[] data;

    public GetBotCallbackAnswer() {
    }

    public GetBotCallbackAnswer(int flags, TLInputPeer peer, int msg_id, byte[] data) {
        this.flags = flags;
        this.peer = peer;
        this.msg_id = msg_id;
        this.data = data;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        msg_id = buffer.readInt();
        if ((flags & (1 << 0)) != 0) {
            data = buffer.readBytes();
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
        if (data.length != 0) {
            flags |= (1 << 0);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeTLObject(peer);
        buff.writeInt(msg_id);
        if ((flags & (1 << 0)) != 0) {
            buff.writeBytes(data);
        }
    }

    public boolean is_game() {
        return (flags & (1 << 1)) != 0;
    }

    public void set_game(boolean v) {
        if (v) {
            flags |= (1 << 1);
        } else {
            flags &= ~(1 << 1);
        }
    }

    public int getConstructor() {
        return ID;
    }
}