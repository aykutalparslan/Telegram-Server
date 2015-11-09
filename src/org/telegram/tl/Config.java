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

import java.util.ArrayList;

public class Config extends TLConfig {

    public static final int ID = 0x4e32b894;

    public int date;
    public int expires;
    public boolean test_mode;
    public int this_dc;
    public TLVector<TLDcOption> dc_options;
    public int chat_size_max;
    public int broadcast_size_max;
    public int forwarded_count_max;
    public int online_update_period_ms;
    public int offline_blur_timeout_ms;
    public int offline_idle_timeout_ms;
    public int online_cloud_timeout_ms;
    public int notify_cloud_delay_ms;
    public int notify_default_delay_ms;
    public int chat_big_size;
    public int push_chat_period_ms;
    public int push_chat_limit;
    public TLVector<TLDisabledFeature> disabled_features;

    public Config() {
        this.dc_options = new TLVector<>();
        this.disabled_features = new TLVector<>();
    }

    public Config(int date, int expires, boolean test_mode, int this_dc, TLVector<TLDcOption> dc_options, int chat_size_max, int broadcast_size_max, int forwarded_count_max, int online_update_period_ms, int offline_blur_timeout_ms, int offline_idle_timeout_ms, int online_cloud_timeout_ms, int notify_cloud_delay_ms, int notify_default_delay_ms, int chat_big_size, int push_chat_period_ms, int push_chat_limit, TLVector<TLDisabledFeature> disabled_features) {
        this.date = date;
        this.expires = expires;
        this.test_mode = test_mode;
        this.this_dc = this_dc;
        this.dc_options = dc_options;
        this.chat_size_max = chat_size_max;
        this.broadcast_size_max = broadcast_size_max;
        this.forwarded_count_max = forwarded_count_max;
        this.online_update_period_ms = online_update_period_ms;
        this.offline_blur_timeout_ms = offline_blur_timeout_ms;
        this.offline_idle_timeout_ms = offline_idle_timeout_ms;
        this.online_cloud_timeout_ms = online_cloud_timeout_ms;
        this.notify_cloud_delay_ms = notify_cloud_delay_ms;
        this.notify_default_delay_ms = notify_default_delay_ms;
        this.chat_big_size = chat_big_size;
        this.push_chat_period_ms = push_chat_period_ms;
        this.push_chat_limit = push_chat_limit;
        this.disabled_features = disabled_features;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        date = buffer.readInt();
        expires = buffer.readInt();
        test_mode = buffer.readBool();
        this_dc = buffer.readInt();
        dc_options = (TLVector<TLDcOption>) buffer.readTLObject(APIContext.getInstance());
        chat_size_max = buffer.readInt();
        broadcast_size_max = buffer.readInt();
        forwarded_count_max = buffer.readInt();
        online_update_period_ms = buffer.readInt();
        offline_blur_timeout_ms = buffer.readInt();
        offline_idle_timeout_ms = buffer.readInt();
        online_cloud_timeout_ms = buffer.readInt();
        notify_cloud_delay_ms = buffer.readInt();
        notify_default_delay_ms = buffer.readInt();
        chat_big_size = buffer.readInt();
        push_chat_period_ms = buffer.readInt();
        push_chat_limit = buffer.readInt();
        disabled_features = (TLVector<TLDisabledFeature>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(128);
        serializeTo(buffer);
        return buffer;
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(date);
        buff.writeInt(expires);
        buff.writeBool(test_mode);
        buff.writeInt(this_dc);
        buff.writeTLObject(dc_options);
        buff.writeInt(chat_size_max);
        buff.writeInt(broadcast_size_max);
        buff.writeInt(forwarded_count_max);
        buff.writeInt(online_update_period_ms);
        buff.writeInt(offline_blur_timeout_ms);
        buff.writeInt(offline_idle_timeout_ms);
        buff.writeInt(online_cloud_timeout_ms);
        buff.writeInt(notify_cloud_delay_ms);
        buff.writeInt(notify_default_delay_ms);
        buff.writeInt(chat_big_size);
        buff.writeInt(push_chat_period_ms);
        buff.writeInt(push_chat_limit);
        buff.writeTLObject(disabled_features);
    }

    public int getConstructor() {
        return ID;
    }
}