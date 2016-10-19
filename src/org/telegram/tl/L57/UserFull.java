/*
 *     This file is part of Telegram Server
 *     Copyright (C) 2015  Aykut Alparslan KOÇ
 *
 *     Telegram Server is free software: you can redistribute it and/or modify
 *     it under the terms of the GNU General Public License as published by
 *     the Free Software Foundation, either version 3 of the License, or
 *     (at your option) any later version.
 *
 *     Telegram Server is distributed in the hope that it will be useful,
 *     but WITHOUT ANY WARRANTY; without even the implied warranty of
 *     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *     GNU General Public License for more details.
 *
 *     You should have received a copy of the GNU General Public License
 *     along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;
import org.telegram.tl.contacts.*;

public class UserFull extends org.telegram.tl.TLUserFull {

    public static final int ID = 0x5932fc03;

    public int flags;
    public org.telegram.tl.TLUser user;
    public String about;
    public TLLink link;
    public org.telegram.tl.photos.TLPhoto profile_photo;
    public org.telegram.tl.TLPeerNotifySettings notify_settings;
    public org.telegram.tl.TLBotInfo bot_info;

    public UserFull() {
    }

    public UserFull(int flags, org.telegram.tl.TLUser user, String about, TLLink link, org.telegram.tl.photos.TLPhoto profile_photo, org.telegram.tl.TLPeerNotifySettings notify_settings, org.telegram.tl.TLBotInfo bot_info) {
        this.flags = flags;
        this.user = user;
        this.about = about;
        this.link = link;
        this.profile_photo = profile_photo;
        this.notify_settings = notify_settings;
        this.bot_info = bot_info;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        user = (org.telegram.tl.TLUser) buffer.readTLObject(APIContext.getInstance());
        if ((flags & (1 << 1)) != 0) {
            about = buffer.readString();
        }
        link = (TLLink) buffer.readTLObject(APIContext.getInstance());
        if ((flags & (1 << 2)) != 0) {
            profile_photo = (org.telegram.tl.photos.TLPhoto) buffer.readTLObject(APIContext.getInstance());
        }
        notify_settings = (org.telegram.tl.TLPeerNotifySettings) buffer.readTLObject(APIContext.getInstance());
        if ((flags & (1 << 3)) != 0) {
            bot_info = (org.telegram.tl.TLBotInfo) buffer.readTLObject(APIContext.getInstance());
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(64);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (about != null && !about.isEmpty()) {
            flags |= (1 << 1);
        }
        if (profile_photo != null) {
            flags |= (1 << 2);
        }
        if (bot_info != null) {
            flags |= (1 << 3);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeTLObject(user);
        if ((flags & (1 << 1)) != 0) {
            buff.writeString(about);
        }
        buff.writeTLObject(link);
        if ((flags & (1 << 2)) != 0) {
            buff.writeTLObject(profile_photo);
        }
        buff.writeTLObject(notify_settings);
        if ((flags & (1 << 3)) != 0) {
            buff.writeTLObject(bot_info);
        }
    }

    public boolean is_blocked() {
        return (flags & (1 << 0)) != 0;
    }

    public void set_blocked(boolean v) {
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