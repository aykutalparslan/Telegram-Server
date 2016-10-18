package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SetBotCallbackAnswer extends TLObject {

    public static final int ID = 0xc927d44b;

    public int flags;
    public long query_id;
    public String message;
    public String url;

    public SetBotCallbackAnswer() {
    }

    public SetBotCallbackAnswer(int flags, long query_id, String message, String url) {
        this.flags = flags;
        this.query_id = query_id;
        this.message = message;
        this.url = url;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        query_id = buffer.readLong();
        if ((flags & (1 << 0)) != 0) {
            message = buffer.readString();
        }
        if ((flags & (1 << 2)) != 0) {
            url = buffer.readString();
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
        if (message != null && !message.isEmpty()) {
            flags |= (1 << 0);
        }
        if (url != null && !url.isEmpty()) {
            flags |= (1 << 2);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeLong(query_id);
        if ((flags & (1 << 0)) != 0) {
            buff.writeString(message);
        }
        if ((flags & (1 << 2)) != 0) {
            buff.writeString(url);
        }
    }

    public boolean is_alert() {
        return (flags & (1 << 1)) != 0;
    }

    public void set_alert(boolean v) {
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