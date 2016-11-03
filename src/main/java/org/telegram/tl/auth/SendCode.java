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

package org.telegram.tl.auth;

import org.telegram.core.TLContext;
import org.telegram.core.TLMethod;
import org.telegram.core.UserStore;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class SendCode extends TLObject implements TLMethod {

    public static final int ID = 1988976461;

    public String phone_number;
    public int sms_type;
    public int api_id;
    public String api_hash;
    public String lang_code;

    public SendCode(){
    }

    public SendCode(String phone_number, int sms_type, int api_id, String api_hash, String lang_code){
        this.phone_number = phone_number;
        this.sms_type = sms_type;
        this.api_id = api_id;
        this.api_hash = api_hash;
        this.lang_code = lang_code;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        phone_number = buffer.readString();
        sms_type = buffer.readInt();
        api_id = buffer.readInt();
        api_hash = buffer.readString();
        lang_code = buffer.readString();
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
        buff.writeString(phone_number);
        buff.writeInt(sms_type);
        buff.writeInt(api_id);
        buff.writeString(api_hash);
        buff.writeString(lang_code);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {

        if (UserStore.getInstance().getUser(clearPhone(phone_number)) == null) {
            return new SentCode(false, "EFEFEFEFEFEFEFEFEF", 120, false);
        } else {
            return new SentCode(true, "EFEFEFEFEFEFEFEFEF", 120, false);
        }
    }

    public String clearPhone(String phone) {
        return phone.replace("(", "").replace(")", "").replace(" ", "").replace("-", "").replace("+", "");
    }
}