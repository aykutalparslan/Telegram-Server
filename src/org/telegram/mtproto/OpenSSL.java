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

package org.telegram.mtproto;

import java.nio.ByteBuffer;

/**
 * Created by aykut on 26/10/16.
 */
public class OpenSSL {
    static {
        System.loadLibrary("OpenSSL");
    }

    public static final int AES_ENCRYPT = 1;
    public static final int AES_DECRYPT = 0;

    public native static void AES_ige_encrypt(ByteBuffer src, byte[] key, byte[] iv, int offset, int length, int enc);
}
