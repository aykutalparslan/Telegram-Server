package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;
import org.telegram.tl.contacts.*;

public class UserFull extends TLUserFull {

    public static final int ID = 0x5a89ac5b;

    public TLUser user;
    public TLLink link;
    public TLPhoto profile_photo;
    public TLPeerNotifySettings notify_settings;
    public boolean blocked;
    public TLBotInfo bot_info;

    public UserFull() {
    }

    public UserFull(TLUser user, TLLink link, TLPhoto profile_photo, TLPeerNotifySettings notify_settings, boolean blocked, TLBotInfo bot_info) {
        this.user = user;
        this.link = link;
        this.profile_photo = profile_photo;
        this.notify_settings = notify_settings;
        this.blocked = blocked;
        this.bot_info = bot_info;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        user = (TLUser) buffer.readTLObject(APIContext.getInstance());
        link = (TLLink) buffer.readTLObject(APIContext.getInstance());
        profile_photo = (TLPhoto) buffer.readTLObject(APIContext.getInstance());
        notify_settings = (TLPeerNotifySettings) buffer.readTLObject(APIContext.getInstance());
        blocked = buffer.readBool();
        bot_info = (TLBotInfo) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(32);
        serializeTo(buffer);
        return buffer;
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeTLObject(user);
        buff.writeTLObject(link);
        buff.writeTLObject(profile_photo);
        buff.writeTLObject(notify_settings);
        buff.writeBool(blocked);
        buff.writeTLObject(bot_info);
    }

    public int getConstructor() {
        return ID;
    }
}