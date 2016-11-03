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

public class Authorization extends org.telegram.tl.auth.TLAuthorization {

    public static final int ID = 0x7bf2e6f6;

    public long hash;
    public int flags;
    public String device_model;
    public String platform;
    public String system_version;
    public int api_id;
    public String app_name;
    public String app_version;
    public int date_created;
    public int date_active;
    public String ip;
    public String country;
    public String region;

    public Authorization() {
    }

    public Authorization(long hash, int flags, String device_model, String platform, String system_version, int api_id, String app_name, String app_version, int date_created, int date_active, String ip, String country, String region) {
        this.hash = hash;
        this.flags = flags;
        this.device_model = device_model;
        this.platform = platform;
        this.system_version = system_version;
        this.api_id = api_id;
        this.app_name = app_name;
        this.app_version = app_version;
        this.date_created = date_created;
        this.date_active = date_active;
        this.ip = ip;
        this.country = country;
        this.region = region;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        hash = buffer.readLong();
        flags = buffer.readInt();
        device_model = buffer.readString();
        platform = buffer.readString();
        system_version = buffer.readString();
        api_id = buffer.readInt();
        app_name = buffer.readString();
        app_version = buffer.readString();
        date_created = buffer.readInt();
        date_active = buffer.readInt();
        ip = buffer.readString();
        country = buffer.readString();
        region = buffer.readString();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(92);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeLong(hash);
        buff.writeInt(flags);
        buff.writeString(device_model);
        buff.writeString(platform);
        buff.writeString(system_version);
        buff.writeInt(api_id);
        buff.writeString(app_name);
        buff.writeString(app_version);
        buff.writeInt(date_created);
        buff.writeInt(date_active);
        buff.writeString(ip);
        buff.writeString(country);
        buff.writeString(region);
    }


    public int getConstructor() {
        return ID;
    }
}