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

package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;
import org.telegram.tl.contacts.*;

public class UserFull extends TLUserFull {

    public static final int ID = 1997575642;

    public TLUser user;
    public TLLink link;
    public TLPhoto profile_photo;
    public TLPeerNotifySettings notify_settings;
    public boolean blocked;
    public String real_first_name;
    public String real_last_name;

    public UserFull(TLUser user, TLLink link, TLPhoto profile_photo, TLPeerNotifySettings notify_settings, boolean blocked, String real_first_name, String real_last_name){
        this.user = user;
        this.link = link;
        this.profile_photo = profile_photo;
        this.notify_settings = notify_settings;
        this.blocked = blocked;
        this.real_first_name = real_first_name;
        this.real_last_name = real_last_name;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        user = (TLUser) buffer.readTLObject(APIContext.getInstance());
        link = (TLLink) buffer.readTLObject(APIContext.getInstance());
        profile_photo = (TLPhoto) buffer.readTLObject(APIContext.getInstance());
        notify_settings = (TLPeerNotifySettings) buffer.readTLObject(APIContext.getInstance());
        blocked = buffer.readBool();
        real_first_name = buffer.readString();
        real_last_name = buffer.readString();
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
        buff.writeString(real_first_name);
        buff.writeString(real_last_name);
    }

    public int getConstructor() {
        return ID;
    }
}