package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class DraftMessage extends org.telegram.tl.TLDraftMessage {

    public static final int ID = 0xfd8e711f;

    public int flags;
    public int reply_to_msg_id;
    public String message;
    public TLVector<org.telegram.tl.TLMessageEntity> entities;
    public int date;

    public DraftMessage() {
        this.entities = new TLVector<>();
    }

    public DraftMessage(int flags, int reply_to_msg_id, String message, TLVector<org.telegram.tl.TLMessageEntity> entities, int date) {
        this.flags = flags;
        this.reply_to_msg_id = reply_to_msg_id;
        this.message = message;
        this.entities = entities;
        this.date = date;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        if ((flags & (1 << 0)) != 0) {
            reply_to_msg_id = buffer.readInt();
        }
        message = buffer.readString();
        if ((flags & (1 << 3)) != 0) {
            entities = (TLVector<org.telegram.tl.TLMessageEntity>) buffer.readTLVector(org.telegram.tl.TLMessageEntity.class);
        }
        date = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(44);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (reply_to_msg_id != 0) {
            flags |= (1 << 0);
        }
        if (entities != null) {
            flags |= (1 << 3);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        if ((flags & (1 << 0)) != 0) {
            buff.writeInt(reply_to_msg_id);
        }
        buff.writeString(message);
        if ((flags & (1 << 3)) != 0) {
            buff.writeTLObject(entities);
        }
        buff.writeInt(date);
    }

    public boolean is_no_webpage() {
        return (flags & (1 << 1)) != 0;
    }

    public void set_no_webpage(boolean v) {
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