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

package org.telegram.tl.L57.account;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SetPrivacy extends TLObject {

    public static final int ID = 0xc9f81ce8;

    public org.telegram.tl.TLInputPrivacyKey key;
    public TLVector<org.telegram.tl.TLInputPrivacyRule> rules;

    public SetPrivacy() {
        this.rules = new TLVector<>();
    }

    public SetPrivacy(org.telegram.tl.TLInputPrivacyKey key, TLVector<org.telegram.tl.TLInputPrivacyRule> rules) {
        this.key = key;
        this.rules = rules;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        key = (org.telegram.tl.TLInputPrivacyKey) buffer.readTLObject(APIContext.getInstance());
        rules = (TLVector<org.telegram.tl.TLInputPrivacyRule>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(20);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeTLObject(key);
        buff.writeTLObject(rules);
    }


    public int getConstructor() {
        return ID;
    }
}