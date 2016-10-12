package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class User extends TLUser {

    public static final int ID = 0x22e49072;

    public int flags;
    public int id;
    public long access_hash;
    public String first_name;
    public String last_name;
    public String username;
    public String phone;
    public TLUserProfilePhoto photo;
    public TLUserStatus status;
    public int bot_info_version;

    public User() {
    }

    public User(int flags, int id, long access_hash, String first_name, String last_name, String username, String phone, TLUserProfilePhoto photo, TLUserStatus status, int bot_info_version) {
        this.flags = flags;
        this.id = id;
        this.access_hash = access_hash;
        this.first_name = first_name;
        this.last_name = last_name;
        this.username = username;
        this.phone = phone;
        this.photo = photo;
        this.status = status;
        this.bot_info_version = bot_info_version;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = buffer.readInt();
        if ((flags & (1 << 0)) != 0) {
            access_hash = buffer.readLong();
        }
        if ((flags & (1 << 1)) != 0) {
            first_name = buffer.readString();
        }
        if ((flags & (1 << 2)) != 0) {
            last_name = buffer.readString();
        }
        if ((flags & (1 << 3)) != 0) {
            username = buffer.readString();
        }
        if ((flags & (1 << 4)) != 0) {
            phone = buffer.readString();
        }
        if ((flags & (1 << 5)) != 0) {
            photo = (TLUserProfilePhoto) buffer.readTLObject(APIContext.getInstance());
        }
        if ((flags & (1 << 6)) != 0) {
            status = (TLUserStatus) buffer.readTLObject(APIContext.getInstance());
        }
        if ((flags & (1 << 14)) != 0) {
            bot_info_version = buffer.readInt();
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
        if (access_hash != 0) {
            flags |= (1 << 0);
        }
        if (first_name != null && !first_name.isEmpty()) {
            flags |= (1 << 1);
        }
        if (last_name != null && !last_name.isEmpty()) {
            flags |= (1 << 2);
        }
        if (username != null && !username.isEmpty()) {
            flags |= (1 << 3);
        }
        if (phone != null && !phone.isEmpty()) {
            flags |= (1 << 4);
        }
        if (photo != null) {
            flags |= (1 << 5);
        }
        if (status != null) {
            flags |= (1 << 6);
        }
        if (bot_info_version != 0) {
            flags |= (1 << 14);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeInt(id);
        if ((flags & (1 << 0)) != 0) {
            buff.writeLong(access_hash);
        }
        if ((flags & (1 << 1)) != 0) {
            buff.writeString(first_name);
        }
        if ((flags & (1 << 2)) != 0) {
            buff.writeString(last_name);
        }
        if ((flags & (1 << 3)) != 0) {
            buff.writeString(username);
        }
        if ((flags & (1 << 4)) != 0) {
            buff.writeString(phone);
        }
        if ((flags & (1 << 5)) != 0) {
            buff.writeTLObject(photo);
        }
        if ((flags & (1 << 6)) != 0) {
            buff.writeTLObject(status);
        }
        if ((flags & (1 << 14)) != 0) {
            buff.writeInt(bot_info_version);
        }
    }

    public boolean is_user_self() {
        return (flags & (1 << 10)) != 0;
    }

    public boolean set_user_self() {
        return (flags |= (1 << 10)) != 0;
    }

    public boolean is_user_contact() {
        return (flags & (1 << 11)) != 0;
    }

    public boolean set_user_contact() {
        return (flags |= (1 << 11)) != 0;
    }

    public boolean is_user_mutual_contact() {
        return (flags & (1 << 12)) != 0;
    }

    public boolean set_user_mutual_contact() {
        return (flags |= (1 << 12)) != 0;
    }

    public boolean is_user_deleted() {
        return (flags & (1 << 13)) != 0;
    }

    public boolean set_user_deleted() {
        return (flags |= (1 << 13)) != 0;
    }

    public boolean is_user_bot() {
        return (flags & (1 << 14)) != 0;
    }

    public boolean set_user_bot() {
        return (flags |= (1 << 14)) != 0;
    }

    public boolean is_user_bot_chat_history() {
        return (flags & (1 << 15)) != 0;
    }

    public boolean set_user_bot_chat_history() {
        return (flags |= (1 << 15)) != 0;
    }

    public boolean is_user_bot_nochats() {
        return (flags & (1 << 16)) != 0;
    }

    public boolean set_user_bot_nochats() {
        return (flags |= (1 << 16)) != 0;
    }

    public boolean is_user_verified() {
        return (flags & (1 << 17)) != 0;
    }

    public boolean set_user_verified() {
        return (flags |= (1 << 17)) != 0;
    }

    public int getConstructor() {
        return ID;
    }
}