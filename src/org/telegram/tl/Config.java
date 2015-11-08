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

public class Config extends TLConfig {

    public static final int ID = 2108568544;

    public int date;
    public int expires;
    public boolean test_mode;
    public int this_dc;
    public TLVector<TLDcOption> dc_options;
    public int chat_big_size;
    public int chat_size_max;
    public int broadcast_size_max;
    public TLVector<TLDisabledFeature> disabled_features;

    public Config() {
        this.dc_options = new TLVector<>();
        this.disabled_features = new TLVector<>();
    }

    public Config(int date, int expires, boolean test_mode, int this_dc, TLVector<TLDcOption> dc_options, int chat_big_size, int chat_size_max, int broadcast_size_max, TLVector<TLDisabledFeature> disabled_features){
        this.date = date;
        this.expires = expires;
        this.test_mode = test_mode;
        this.this_dc = this_dc;
        this.dc_options = dc_options;
        this.chat_big_size = chat_big_size;
        this.chat_size_max = chat_size_max;
        this.broadcast_size_max = broadcast_size_max;
        this.disabled_features = disabled_features;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        date = buffer.readInt();
        expires = buffer.readInt();
        test_mode = buffer.readBool();
        this_dc = buffer.readInt();
        dc_options = (TLVector<TLDcOption>) buffer.readTLObject(APIContext.getInstance());
        chat_big_size = buffer.readInt();
        chat_size_max = buffer.readInt();
        broadcast_size_max = buffer.readInt();
        disabled_features = (TLVector<TLDisabledFeature>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeInt(date);
        buff.writeInt(expires);
        buff.writeBool(test_mode);
        buff.writeInt(this_dc);
        buff.writeTLObject(dc_options);
        buff.writeInt(chat_big_size);
        buff.writeInt(chat_size_max);
        buff.writeInt(broadcast_size_max);
        buff.writeTLObject(disabled_features);
    }

    public int getConstructor() {
        return ID;
    }
}