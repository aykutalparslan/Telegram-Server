package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SaveRecentSticker extends TLObject {

    public static final int ID = 0x392718f8;

    public int flags;
    public TLInputDocument id;
    public boolean unsave;

    public SaveRecentSticker() {
    }

    public SaveRecentSticker(int flags, TLInputDocument id, boolean unsave) {
        this.flags = flags;
        this.id = id;
        this.unsave = unsave;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = (TLInputDocument) buffer.readTLObject(APIContext.getInstance());
        unsave = buffer.readBool();
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
        buff.writeTLObject(id);
        buff.writeBool(unsave);
    }

    public boolean is_attached() {
        return (flags & (1 << 0)) != 0;
    }

    public void set_attached(boolean v) {
        if (v) {
            flags |= (1 << 0);
        } else {
            flags &= ~(1 << 0);
        }
    }

    public int getConstructor() {
        return ID;
    }
}