package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class BotCallbackAnswer extends TLBotCallbackAnswer {

    public static final int ID = 0xb10df1fb;

    public int flags;
    public String message;
    public String url;

    public BotCallbackAnswer() {
    }

    public BotCallbackAnswer(int flags, String message, String url) {
        this.flags = flags;
        this.message = message;
        this.url = url;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
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

    public boolean is_has_url() {
        return (flags & (1 << 3)) != 0;
    }

    public void set_has_url(boolean v) {
        if (v) {
            flags |= (1 << 3);
        } else {
            flags &= ~(1 << 3);
        }
    }

    public int getConstructor() {
        return ID;
    }
}