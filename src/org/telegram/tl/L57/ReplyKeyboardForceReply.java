package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ReplyKeyboardForceReply extends org.telegram.tl.TLReplyMarkup {

    public static final int ID = 0xf4108aa0;

    public int flags;

    public ReplyKeyboardForceReply() {
    }

    public ReplyKeyboardForceReply(int flags) {
        this.flags = flags;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(24);
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
    }

    public boolean is_single_use() {
        return (flags & (1 << 1)) != 0;
    }

    public void set_single_use(boolean v) {
        if (v) {
            flags |= (1 << 1);
        } else {
            flags &= ~(1 << 1);
        }
    }

    public boolean is_selective() {
        return (flags & (1 << 2)) != 0;
    }

    public void set_selective(boolean v) {
        if (v) {
            flags |= (1 << 2);
        } else {
            flags &= ~(1 << 2);
        }
    }

    public int getConstructor() {
        return ID;
    }
}