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

public class Config extends org.telegram.tl.TLConfig {

    public static final int ID = 0x9a6b2e2a;

    public int flags;
    public int date;
    public int expires;
    public boolean test_mode;
    public int this_dc;
    public TLVector<org.telegram.tl.TLDcOption> dc_options;
    public int chat_size_max;
    public int megagroup_size_max;
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
    public int saved_gifs_limit;
    public int edit_time_limit;
    public int rating_e_decay;
    public int stickers_recent_limit;
    public int tmp_sessions;
    public TLVector<org.telegram.tl.TLDisabledFeature> disabled_features;

    public Config() {
        this.dc_options = new TLVector<>();
        this.disabled_features = new TLVector<>();
    }

    public Config(int flags, int date, int expires, boolean test_mode, int this_dc, TLVector<org.telegram.tl.TLDcOption> dc_options, int chat_size_max, int megagroup_size_max, int forwarded_count_max, int online_update_period_ms, int offline_blur_timeout_ms, int offline_idle_timeout_ms, int online_cloud_timeout_ms, int notify_cloud_delay_ms, int notify_default_delay_ms, int chat_big_size, int push_chat_period_ms, int push_chat_limit, int saved_gifs_limit, int edit_time_limit, int rating_e_decay, int stickers_recent_limit, int tmp_sessions, TLVector<org.telegram.tl.TLDisabledFeature> disabled_features) {
        this.flags = flags;
        this.date = date;
        this.expires = expires;
        this.test_mode = test_mode;
        this.this_dc = this_dc;
        this.dc_options = dc_options;
        this.chat_size_max = chat_size_max;
        this.megagroup_size_max = megagroup_size_max;
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
        this.saved_gifs_limit = saved_gifs_limit;
        this.edit_time_limit = edit_time_limit;
        this.rating_e_decay = rating_e_decay;
        this.stickers_recent_limit = stickers_recent_limit;
        this.tmp_sessions = tmp_sessions;
        this.disabled_features = disabled_features;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        date = buffer.readInt();
        expires = buffer.readInt();
        test_mode = buffer.readBool();
        this_dc = buffer.readInt();
        dc_options = (TLVector<org.telegram.tl.TLDcOption>) buffer.readTLObject(APIContext.getInstance());
        chat_size_max = buffer.readInt();
        megagroup_size_max = buffer.readInt();
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
        saved_gifs_limit = buffer.readInt();
        edit_time_limit = buffer.readInt();
        rating_e_decay = buffer.readInt();
        stickers_recent_limit = buffer.readInt();
        if ((flags & (1 << 0)) != 0) {
            tmp_sessions = buffer.readInt();
        }
        disabled_features = (TLVector<org.telegram.tl.TLDisabledFeature>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(109);
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
        buff.writeInt(date);
        buff.writeInt(expires);
        buff.writeBool(test_mode);
        buff.writeInt(this_dc);
        buff.writeTLObject(dc_options);
        buff.writeInt(chat_size_max);
        buff.writeInt(megagroup_size_max);
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
        buff.writeInt(saved_gifs_limit);
        buff.writeInt(edit_time_limit);
        buff.writeInt(rating_e_decay);
        buff.writeInt(stickers_recent_limit);
        if ((flags & (1 << 0)) != 0) {
            buff.writeInt(tmp_sessions);
        }
        buff.writeTLObject(disabled_features);
    }


    public int getConstructor() {
        return ID;
    }
}