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

import org.junit.Test;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.pq.req_pq;

import java.util.Random;

import static org.junit.Assert.*;

/**
 * Created by aykut on 01/12/15.
 */
public class APIContextTest {
    @Test
    public void deserializes_req_pq() {
        ProtocolBuffer buffer = new ProtocolBuffer(1);
        byte[] nonce = new byte[]{1, 4, 34, -23, 23, 66, 9, 0, 11, 90, 123, -67, 87, 2, -45, 8};
        ;
        req_pq a = new req_pq(nonce);
        buffer.writeTLObject(a);
        TLObject b = buffer.readTLObject(APIContext.getInstance());
        assertTrue(b instanceof req_pq);
        assertArrayEquals(nonce, ((req_pq) b).nonce);
    }

}