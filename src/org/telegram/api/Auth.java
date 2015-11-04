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

package org.telegram.api;

import org.telegram.tl.auth.*;

/**
 * Created by aykut on 04/11/15.
 */
public class Auth {
    public static TLCheckedPhone checkPhone(String phone_number){
        return new CheckedPhone(false, false);
    }

    public static TLSentCode sendCode(String phone_number, int sms_type, int api_id, String api_hash, String lang_code){
        return new SentCode(false, "", 60, false);
    }
}
