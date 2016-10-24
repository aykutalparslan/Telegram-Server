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

import org.junit.Test;
import org.telegram.tl.APIContext;
import org.telegram.tl.DeserializationContext;
import org.telegram.tl.TLObject;
import org.telegram.tl.service.Ping;
import org.telegram.tl.service.rpc_error;

import static org.junit.Assert.*;

/**
 * Created by aykut on 30/11/15.
 */
public class ProtocolBufferTest {
    @Test
    public void writesAndReadsByte() {
        ProtocolBuffer buffer = new ProtocolBuffer(1);
        byte a = 78;
        buffer.writeByte(a);
        byte b = buffer.readByte();
        assertEquals(a, b);
    }

    @Test
    public void writesAndReadsInt() {
        ProtocolBuffer buffer = new ProtocolBuffer(1);
        int a = 935513460;
        buffer.writeInt(a);
        int b = buffer.readInt();
        assertEquals(a, b);
    }

    @Test
    public void writesAndReadsLong() {
        ProtocolBuffer buffer = new ProtocolBuffer(1);
        long a = 935513460494946689L;
        buffer.writeLong(a);
        long b = buffer.readLong();
        assertEquals(a, b);
    }

    @Test
    public void writesAndReadsDouble() {
        ProtocolBuffer buffer = new ProtocolBuffer(1);
        double a = 9355134.12341233;
        buffer.writeDouble(a);
        double b = buffer.readDouble();
        assertEquals(0, Double.compare(a, b));
    }

    @Test
    public void writesAndReadsBool() {
        ProtocolBuffer buffer = new ProtocolBuffer(1);
        boolean a = true;
        buffer.writeBool(a);
        boolean b = buffer.readBool();
        assertEquals(a, b);
    }

    @Test
    public void writesAndReadsString() {
        ProtocolBuffer buffer = new ProtocolBuffer(1);
        String a = "asdfaslkdjfalsjkdfhlkasjdf";
        buffer.writeString(a);
        String b = buffer.readString();
        assertEquals(a, b);
        a = "";
        buffer.writeString(a);
        b = buffer.readString();
        assertEquals(a, b);
    }

    @Test
    public void writesAndReadsBytes() {
        ProtocolBuffer buffer = new ProtocolBuffer(1);
        byte[] a = new byte[]{1, 4, 34, -23, 23, 66, 9};
        buffer.writeBytes(a);
        byte[] b = buffer.readBytes();
        assertArrayEquals(a, b);
    }

    @Test
    public void writesAndReadsRawBytes() {
        ProtocolBuffer buffer = new ProtocolBuffer(1);
        byte[] a = new byte[]{1, 4, 34, -23, 23, 66, 9};
        buffer.write(a);
        byte[] b = buffer.read(buffer.length());
        assertArrayEquals(a, b);
    }

    @Test
    public void writesAndReadsTLObject() {
        ProtocolBuffer buffer = new ProtocolBuffer(1);
        rpc_error a = new rpc_error(401, "UNAUTHORIZED");
        buffer.writeTLObject(a);
        rpc_error b = (rpc_error) buffer.readTLObject(new DeserializationContext() {
            @Override
            public <T extends TLObject> void addToSchema(Class<T> type) {

            }

            @Override
            public TLObject deserialize(ProtocolBuffer buffer) {
                buffer.readInt();
                rpc_error error = new rpc_error();
                error.deserialize(buffer);
                return error;
            }
        });
        assertEquals(a.error_code, b.error_code);
        assertEquals(a.error_code, b.error_code);
    }
}