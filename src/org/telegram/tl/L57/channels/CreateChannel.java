package org.telegram.tl.L57.channels;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class CreateChannel extends TLObject {

    public static final int ID = 0xf4893d7f;

    public int flags;
    public String title;
    public String about;

    public CreateChannel() {
    }

    public CreateChannel(int flags, String title, String about) {
        this.flags = flags;
        this.title = title;
        this.about = about;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        title = buffer.readString();
        about = buffer.readString();
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
        buff.writeString(title);
        buff.writeString(about);
    }

    public boolean is_broadcast() {
        return (flags & (1 << 0)) != 0;
    }

    public void set_broadcast(boolean v) {
        if (v) {
            flags |= (1 << 0);
        } else {
            flags &= ~(1 << 0);
        }
    }

    public boolean is_megagroup() {
        return (flags & (1 << 1)) != 0;
    }

    public void set_megagroup(boolean v) {
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