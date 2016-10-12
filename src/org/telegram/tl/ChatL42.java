package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class ChatL42 extends TLChat {

    public static final int ID = 0xd91cdd54;

    public int flags;
    public int id;
    public String title;
    public TLChatPhoto photo;
    public int participants_count;
    public int date;
    public int version;
    public TLInputChannel migrated_to;

    public ChatL42() {
    }

    public ChatL42(int flags, int id, String title, TLChatPhoto photo, int participants_count, int date, int version, TLInputChannel migrated_to) {
        this.flags = flags;
        this.id = id;
        this.title = title;
        this.photo = photo;
        this.participants_count = participants_count;
        this.date = date;
        this.version = version;
        this.migrated_to = migrated_to;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = buffer.readInt();
        title = buffer.readString();
        photo = (TLChatPhoto) buffer.readTLObject(APIContext.getInstance());
        participants_count = buffer.readInt();
        date = buffer.readInt();
        version = buffer.readInt();
        if ((flags & (1 << 6)) != 0) {
            migrated_to = (TLInputChannel) buffer.readTLObject(APIContext.getInstance());
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
        if (migrated_to != null) {
            flags |= (1 << 6);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeInt(id);
        buff.writeString(title);
        buff.writeTLObject(photo);
        buff.writeInt(participants_count);
        buff.writeInt(date);
        buff.writeInt(version);
        if ((flags & (1 << 6)) != 0) {
            buff.writeTLObject(migrated_to);
        }
    }

    public boolean is_chatL42_creator() {
        return (flags & (1 << 0)) != 0;
    }

    public boolean set_chatL42_creator() {
        return (flags |= (1 << 0)) != 0;
    }

    public boolean is_chatL42_kicked() {
        return (flags & (1 << 1)) != 0;
    }

    public boolean set_chatL42_kicked() {
        return (flags |= (1 << 1)) != 0;
    }

    public boolean is_chatL42_left() {
        return (flags & (1 << 2)) != 0;
    }

    public boolean set_chatL42_left() {
        return (flags |= (1 << 2)) != 0;
    }

    public boolean is_chatL42_admins_enabled() {
        return (flags & (1 << 3)) != 0;
    }

    public boolean set_chatL42_admins_enabled() {
        return (flags |= (1 << 3)) != 0;
    }

    public boolean is_chatL42_admin() {
        return (flags & (1 << 4)) != 0;
    }

    public boolean set_chatL42_admin() {
        return (flags |= (1 << 4)) != 0;
    }

    public boolean is_chatL42_deactivated() {
        return (flags & (1 << 5)) != 0;
    }

    public boolean set_chatL42_deactivated() {
        return (flags |= (1 << 5)) != 0;
    }

    public int getConstructor() {
        return ID;
    }
}