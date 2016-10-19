package org.telegram.tl.L57.auth;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class Authorization extends org.telegram.tl.auth.TLAuthorization {

    public static final int ID = 0xcd050916;

    public int flags;
    public int tmp_sessions;
    public org.telegram.tl.TLUser user;

    public Authorization() {
    }

    public Authorization(int flags, int tmp_sessions, org.telegram.tl.TLUser user) {
        this.flags = flags;
        this.tmp_sessions = tmp_sessions;
        this.user = user;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        if ((flags & (1 << 0)) != 0) {
            tmp_sessions = buffer.readInt();
        }
        user = (org.telegram.tl.TLUser) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(24);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (tmp_sessions != 0) {
            flags |= (1 << 0);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        if ((flags & (1 << 0)) != 0) {
            buff.writeInt(tmp_sessions);
        }
        buff.writeTLObject(user);
    }


    public int getConstructor() {
        return ID;
    }
}