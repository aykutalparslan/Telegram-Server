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
import org.telegram.core.AuthKeyStore;
import org.telegram.core.TLContext;
import org.telegram.data.AuthKeyModel;
import org.telegram.mtproto.MessageKeyData;
import org.telegram.mtproto.OpenSSL;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.mtproto.secure.CryptoUtils;
import org.telegram.tl.APIContext;
import org.telegram.tl.TLObject;

import java.io.File;
import java.io.FileOutputStream;
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

        /*byte[] rawRequest = new byte[currentPacketLength];
        in.readBytes(rawRequest);
        in.readerIndex(in.readerIndex() - currentPacketLength);*/


        ProtocolBuffer buffer = new ProtocolBuffer(in);

        MTProtoMessage message = new MTProtoMessage();
        message.auth_key_id = buffer.readLong();
        if (message.auth_key_id == 0) {
            message.message_id = buffer.readLong();
            message.message_data_length = buffer.readInt();
            message.message_data = APIContext.getInstance().deserialize(buffer);

            out.add(message);
        } else {
            byte[] message_key = buffer.read(16);
            int encrypted_data_length = currentPacketLength - (8 + 16);
            byte[] encrypted_bytes = buffer.read(encrypted_data_length);

            AuthKeyModel authKey = AuthKeyStore.getInstance().getAuthKey(message.auth_key_id);

            if (authKey.auth_key != null) {
                ProtocolBuffer buff = decryptRpc(authKey.auth_key, encrypted_bytes, message_key);

                message.server_salt = buff.readLong();
                message.session_id = buff.readLong();
                message.message_id = buff.readLong();
                message.seq_no = buff.readInt();
                message.message_data_length = buff.readInt();
                message.message_data = APIContext.getInstance().deserialize(buff);
                buff.release();

                out.add(message);

                /*String dir = "dump/";
                File theDir = new File(dir);
                if (!theDir.exists()) {
                    try{
                        theDir.mkdir();
                    }
                    catch(SecurityException se){
                    }
                }
                try{
                    FileOutputStream fos = new FileOutputStream(dir+ message.message_data.toString());
                    fos.write(rawRequest);
                    fos.close();
                } catch(Exception se){
                }*/

            }
        }

        currentPacketLength = 0;
    }

    private ProtocolBuffer decryptRpc(byte[] authKey, byte[] bytes, byte[] messageKey) {
        MessageKeyData keyData = MessageKeyData.generateMessageKeyData(authKey, messageKey, false);
        byte[] decryptedData = CryptoUtils.AES256IGEDecrypt(bytes, keyData.aesIv, keyData.aesKey);
        ProtocolBuffer buff = new ProtocolBuffer(decryptedData);
        return buff;
    }
}
