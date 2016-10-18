package org.telegram.tl.L57.account;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateProfile extends TLObject {

    public static final int ID = 0x78515775;

    public int flags;
    public String first_name;
    public String last_name;
    public String about;

    public UpdateProfile() {
    }

    public UpdateProfile(int flags, String first_name, String last_name, String about) {
        this.flags = flags;
        this.first_name = first_name;
        this.last_name = last_name;
        this.about = about;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        if ((flags & (1 << 0)) != 0) {
            first_name = buffer.readString();
        }
        if ((flags & (1 << 1)) != 0) {
            last_name = buffer.readString();
        }
        if ((flags & (1 << 2)) != 0) {
            about = buffer.readString();
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
        if (first_name != null && !first_name.isEmpty()) {
            flags |= (1 << 0);
        }
        if (last_name != null && !last_name.isEmpty()) {
            flags |= (1 << 1);
        }
        if (about != null && !about.isEmpty()) {
            flags |= (1 << 2);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        if ((flags & (1 << 0)) != 0) {
            buff.writeString(first_name);
        }
        if ((flags & (1 << 1)) != 0) {
            buff.writeString(last_name);
        }
        if ((flags & (1 << 2)) != 0) {
            buff.writeString(about);
        }
    }


    public int getConstructor() {
        return ID;
    }
}