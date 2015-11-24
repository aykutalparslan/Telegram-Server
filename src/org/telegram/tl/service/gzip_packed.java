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

package org.telegram.tl.service;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class gzip_packed extends TLObject {

    public static final int ID = 0x3072cfa1;

    public byte[] packed_data;

    public gzip_packed() {

    }

    public gzip_packed(byte[] packed_data) {
        this.packed_data = packed_data;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        packed_data = buffer.readBytes();
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
        buff.writeBytes(packed_data);
    }

    public byte[] getUncompressed() {
        try {
            java.util.zip.Inflater inf = new java.util.zip.Inflater();
            java.io.ByteArrayInputStream bytein = new java.io.ByteArrayInputStream(packed_data);
            java.util.zip.GZIPInputStream gzin = new java.util.zip.GZIPInputStream(bytein);
            java.io.ByteArrayOutputStream byteout = new java.io.ByteArrayOutputStream();

            int res = 0;
            byte buf[] = new byte[1024];
            while (res >= 0) {
                res = gzin.read(buf, 0, buf.length);
                if (res > 0) {
                    byteout.write(buf, 0, res);
                }
            }
            return byteout.toByteArray();
        } catch (Exception e) {
            return null;
        }
    }

    public int getConstructor() {
        return ID;
    }
}