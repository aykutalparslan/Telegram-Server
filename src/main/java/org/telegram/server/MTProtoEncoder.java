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

package org.telegram.server;

import io.netty.buffer.ByteBuf;
import io.netty.channel.ChannelHandlerContext;
import io.netty.handler.codec.MessageToByteEncoder;
import org.telegram.mtproto.ProtocolBuffer;

/**
 * Created by aykut on 25/09/15.
 */
public class MTProtoEncoder extends MessageToByteEncoder<ProtocolBuffer> {
    @Override
    protected void encode(ChannelHandlerContext ctx, ProtocolBuffer buff, ByteBuf out) {
        boolean abridged = true;

        int bufferLen = buff.length();
        int packetLength = bufferLen / 4;

        if (packetLength < 0x7f) {
            bufferLen++;
        } else {
            bufferLen += 4;
        }

        if(abridged){
            boolean reportAck = false;

            if (packetLength < 0x7f) {
                if (reportAck) {
                    packetLength |= (1 << 7);
                }
                out.writeByte(packetLength);
            } else {
                packetLength = (packetLength << 8) + 0x7f;
                if (reportAck) {
                    packetLength |= (1 << 7);
                }
                out.writeIntLE(packetLength);
            }
        } else{
            packetLength = bufferLen;
            int packetNum=1;

            out.writeIntLE(packetLength);
            out.writeIntLE(packetNum);
        }

        out.writeBytes(buff.getBuffer());

        buff.release();
    }
}
