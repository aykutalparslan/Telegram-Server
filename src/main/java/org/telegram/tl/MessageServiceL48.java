package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class MessageServiceL48 extends TLMessage {

    public static final int ID = 0x9e19a1f6;

    public int flags;
    public int id;
    public int from_id;
    public TLPeer to_id;
    public int reply_to_msg_id;
    public int date;
    public TLMessageAction action;

    public MessageServiceL48() {
    }

    public MessageServiceL48(int flags, int id, int from_id, TLPeer to_id, int reply_to_msg_id, int date, TLMessageAction action) {
        this.flags = flags;
        this.id = id;
        this.from_id = from_id;
        this.to_id = to_id;
        this.reply_to_msg_id = reply_to_msg_id;
        this.date = date;
        this.action = action;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = buffer.readInt();
        if ((flags & (1 << 8)) != 0) {
            from_id = buffer.readInt();
        }
        to_id = (TLPeer) buffer.readTLObject(APIContext.getInstance());
        if ((flags & (1 << 3)) != 0) {
            reply_to_msg_id = buffer.readInt();
        }
        date = buffer.readInt();
        action = (TLMessageAction) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(32);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (from_id != 0) {
            flags |= (1 << 8);
        }
        if (reply_to_msg_id != 0) {
            flags |= (1 << 3);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeInt(id);
        if ((flags & (1 << 8)) != 0) {
            buff.writeInt(from_id);
        }
        buff.writeTLObject(to_id);
        if ((flags & (1 << 3)) != 0) {
            buff.writeInt(reply_to_msg_id);
        }
        buff.writeInt(date);
        buff.writeTLObject(action);
    }

    public boolean is_messageServiceL48_out() {
        return (flags & (1 << 1)) != 0;
    }

    public boolean set_messageServiceL48_out() {
        return (flags |= (1 << 1)) != 0;
    }

    public boolean is_messageServiceL48_mentioned() {
        return (flags & (1 << 4)) != 0;
    }

    public boolean set_messageServiceL48_mentioned() {
        return (flags |= (1 << 4)) != 0;
    }

    public boolean is_messageServiceL48_media_unread() {
        return (flags & (1 << 5)) != 0;
    }

    public boolean set_messageServiceL48_media_unread() {
        return (flags |= (1 << 5)) != 0;
    }

    public boolean is_messageServiceL48_silent() {
        return (flags & (1 << 13)) != 0;
    }

    public boolean set_messageServiceL48_silent() {
        return (flags |= (1 << 13)) != 0;
    }

    public boolean is_messageServiceL48_post() {
        return (flags & (1 << 14)) != 0;
    }

    public boolean set_messageServiceL48_post() {
        return (flags |= (1 << 14)) != 0;
    }

    public int getConstructor() {
        return ID;
    }
}