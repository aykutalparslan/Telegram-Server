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
import io.netty.handler.codec.ByteToMessageDecoder;
import org.telegram.mtproto.ProtocolBuffer;

import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.util.List;

/**
 * Created by aykut on 25/09/15.
 */
public class MTProtoDecoder  extends ByteToMessageDecoder {
    int currentPacketLength;
    byte fByte;

    @Override
    protected void decode(ChannelHandlerContext ctx, ByteBuf in, List<Object> out) {
        if (currentPacketLength == 0) {
            if (in.readableBytes() < 1) {
                return;
            }

            fByte = in.readByte();
            if ((byte) 0xef == fByte) {
                if (in.readableBytes() < 1) {
                    return;
                }
                fByte = in.readByte();
            }
            if (fByte != 0x7f && fByte != -1) {
                if (fByte < 0) {
                    currentPacketLength = ((int) fByte + 128) * 4;
                } else {
                    currentPacketLength = (int) fByte * 4;
                }
            } else {
                if (in.readableBytes() < 3) {
                    return;
                }
                byte[] lenBytes = new byte[4];
                lenBytes[3] = in.readByte();
                lenBytes[2] = in.readByte();
                lenBytes[1] = in.readByte();
                ByteBuffer wrapped = ByteBuffer.wrap(lenBytes);
                int len = wrapped.getInt();
                currentPacketLength = len * 4;
            }
        }

        if (in.readableBytes() < currentPacketLength) {
            if (in.capacity() < currentPacketLength) {
                in.capacity(in.capacity() + currentPacketLength);
            }
            return;
        }

        ProtocolBuffer received = new ProtocolBuffer(currentPacketLength);
        byte[] bytes = new byte[currentPacketLength];

        in.readBytes(bytes, 0, currentPacketLength);
        received.write(bytes);

        out.add(received);
        currentPacketLength = 0;
    }
}
