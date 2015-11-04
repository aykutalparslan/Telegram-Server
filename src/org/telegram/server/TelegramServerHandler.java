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

import io.netty.channel.ChannelHandlerContext;
import io.netty.channel.ChannelInboundHandlerAdapter;
import org.telegram.api.TLContext;
import org.telegram.api.TLMethod;
import org.telegram.mtproto.MTProtoAuth;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.APIContext;
import org.telegram.tl.TLObject;
import org.telegram.tl.pq.req_DH_params;
import org.telegram.tl.pq.req_pq;
import org.telegram.tl.pq.set_client_DH_params;

/**
 * Created by aykut on 28/09/15.
 */
public class TelegramServerHandler extends ChannelInboundHandlerAdapter {
    private MTProtoAuth auth;
    private long lastOutgoingMessageId;
    private TLContext tlContext = new TLContext();



    @Override
    public void handlerAdded(ChannelHandlerContext ctx) throws Exception {
        auth = new MTProtoAuth();
    }

    @Override
    public void channelActive(ChannelHandlerContext ctx) {

    }

    @Override
    public void channelRead(ChannelHandlerContext ctx, Object msg) throws Exception {
        super.channelRead(ctx, msg);
        ProtocolBuffer data = (ProtocolBuffer)msg;
        long keyId = data.readLong();
        if (keyId == 0) {
            long messageId = data.readLong();
            data.readInt();
            TLObject message = APIContext.getInstance().deserialize(data);

            if(message instanceof req_pq){
                ctx.writeAndFlush(auth.msgs_ack(messageId));
                ctx.writeAndFlush(auth.resPQ((req_pq)message));
            } else if (message instanceof req_DH_params){
                ctx.writeAndFlush(auth.msgs_ack(messageId));
                ctx.writeAndFlush(auth.server_DH_params((req_DH_params)message));
            } else if (message instanceof set_client_DH_params){
                ctx.writeAndFlush(auth.msgs_ack(messageId));
                ctx.writeAndFlush(auth.set_client_DH_params((set_client_DH_params) message));
            }
        } else {
            if(getTlContext().getAuthKeyId() == 0){
                getTlContext().setAuthKeyId(keyId);
            }
            byte[] message_key = data.read(16);
            byte[] encrypted_bytes = data.read(data.length() - (8 + 16));

            ProtocolBuffer buff = auth.decryptRpc(encrypted_bytes, message_key);

            long server_salt = buff.readLong();
            long message_id = buff.readLong();
            int seqNo = buff.readInt();
            int len = buff.readInt();

            TLObject rpc = APIContext.getInstance().deserialize(buff);

            if(rpc instanceof TLMethod){
                ctx.writeAndFlush(auth.msgs_ack(message_id));
                TLObject res = ((TLMethod) rpc).execute(getTlContext(), generateMessageId(), message_id);
                if(res != null){
                    ctx.writeAndFlush(res.serialize());
                }
            }
        }
    }

    long generateMessageId() {
        long messageId = (long) ((((double) System.currentTimeMillis()) * 4294967296.0) / 1000.0);
        if (messageId <= lastOutgoingMessageId) {
            messageId = lastOutgoingMessageId + 1;
        }
        while (messageId % 4 != 0) {
            messageId++;
        }

        lastOutgoingMessageId = messageId;
        return messageId;
    }

    @Override
    public void exceptionCaught(ChannelHandlerContext ctx, Throwable cause) {
        cause.printStackTrace();
        ctx.close();
    }

    public TLContext getTlContext() {
        return tlContext;
    }
}
