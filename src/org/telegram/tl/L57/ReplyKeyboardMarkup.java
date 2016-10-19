package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ReplyKeyboardMarkup extends org.telegram.tl.TLReplyMarkup {

    public static final int ID = 0x3502758c;

    public int flags;
    public TLVector<org.telegram.tl.TLKeyboardButtonRow> rows;

    public ReplyKeyboardMarkup() {
        this.rows = new TLVector<>();
    }

    public ReplyKeyboardMarkup(int flags, TLVector<org.telegram.tl.TLKeyboardButtonRow> rows) {
        this.flags = flags;
        this.rows = rows;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        rows = (TLVector<org.telegram.tl.TLKeyboardButtonRow>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(40);
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
        buff.writeTLObject(rows);
    }

    public boolean is_resize() {
        return (flags & (1 << 0)) != 0;
    }

    public void set_resize(boolean v) {
        if (v) {
            flags |= (1 << 0);
        } else {
            flags &= ~(1 << 0);
        }
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