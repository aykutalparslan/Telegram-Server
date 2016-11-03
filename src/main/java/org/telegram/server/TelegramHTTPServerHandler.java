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
import io.netty.buffer.Unpooled;
import io.netty.channel.ChannelFuture;
import io.netty.channel.ChannelFutureListener;
import io.netty.channel.ChannelHandlerContext;
import io.netty.channel.ChannelInboundHandlerAdapter;
import io.netty.handler.codec.http.*;
import io.netty.util.CharsetUtil;
import io.netty.util.ReferenceCounted;
import org.telegram.core.*;
import org.telegram.mtproto.MTProtoAuth;
import org.telegram.mtproto.MessageKeyData;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.mtproto.Utilities;
import org.telegram.mtproto.secure.CryptoUtils;
import org.telegram.tl.*;
import org.telegram.tl.auth.Authorization;
import org.telegram.tl.auth.SignIn;
import org.telegram.tl.pq.req_DH_params;
import org.telegram.tl.pq.req_pq;
import org.telegram.tl.pq.set_client_DH_params;
import org.telegram.tl.service.*;

import java.util.Random;

/**
 * Created by aykut on 28/09/15.
 */
public class TelegramHTTPServerHandler extends ChannelInboundHandlerAdapter {
    private long lastOutgoingMessageId;
    private TLContext tlContext;
    private int seq_no = 0;
    private long unique_session = 0;

    @Override
    public void handlerAdded(ChannelHandlerContext ctx) throws Exception {
        tlContext = new TLContext();
    }

    @Override
    public void channelActive(ChannelHandlerContext ctx) {

    }

    @Override
    public void channelRead(ChannelHandlerContext ctx, Object msg) throws Exception {
        FullHttpRequest request = (FullHttpRequest) msg;
        /*ByteBuf byteBuf = request.content();

        byte[] message_bytes = new byte[byteBuf.readableBytes()];
        byteBuf.readBytes(message_bytes);*/
        ProtocolBuffer data = new ProtocolBuffer(request.content());
        long keyId = data.readLong();
        if (keyId == 0) {
            long messageId = data.readLong();
            data.readInt();
            TLObject message = APIContext.getInstance().deserialize(data);
            if (message != null) {
                System.out.println("TLObject:" + message.toString());
                if (message instanceof msg_container) {
                    for (message m : ((msg_container) message).messages) {
                        //processRPC(ctx, m.body, m.msg_id);
                    }
                }
            } else {
                System.out.println("null message");
            }

            MTProtoAuth auth2 = null;

            if (message instanceof req_pq) {
                auth2 = ProtoAuthStore.getInstance().getProtoAuth(((req_pq) message).nonce);
                sendResponse(ctx, auth2.resPQ((req_pq) message).getBytes());
                ProtoAuthStore.getInstance().updateProtoAuth(((req_pq) message).nonce, auth2);
            } else if (message instanceof req_DH_params) {
                auth2 = ProtoAuthStore.getInstance().getProtoAuth(((req_DH_params) message).nonce);
                sendResponse(ctx, auth2.server_DH_params((req_DH_params) message).getBytes());
                ProtoAuthStore.getInstance().updateProtoAuth(((req_DH_params) message).nonce, auth2);
            } else if (message instanceof set_client_DH_params) {
                auth2 = ProtoAuthStore.getInstance().getProtoAuth(((set_client_DH_params) message).nonce);
                sendResponse(ctx, auth2.set_client_DH_params((set_client_DH_params) message).getBytes());
                ProtoAuthStore.getInstance().removeProtoAuth(((set_client_DH_params) message).nonce);
            }
        } else {
            if (tlContext.getAuthKeyId() == 0) {
                tlContext.setAuthKeyId(keyId);
            }
            //try {
            byte[] message_key = data.read(16);
            byte[] encrypted_bytes = data.read(data.length() - (8 + 16));

            ProtocolBuffer buff = decryptRpc(tlContext, encrypted_bytes, message_key);

            long server_salt = buff.readLong();
            long session_id = buff.readLong();
            long message_id = buff.readLong();
            int seqNo = buff.readInt();
            int len = buff.readInt();

            tlContext.setSessionId(session_id);

            if (unique_session == 0) {
                create_new_session(ctx, message_id);
            }

            TLObject rpc = APIContext.getInstance().deserialize(buff);
            processRPC(ctx, rpc, message_id);
            //} catch (Exception e){

            //}

        }
    }

    private void create_new_session(ChannelHandlerContext ctx, long message_id) {
        Random rnd = new Random();
        unique_session = rnd.nextLong();
        new_session_created newSessionCreated = new new_session_created(message_id, unique_session, ServerSaltStore.getInstance().getServerSalt(tlContext.getAuthKeyId()));
        ctx.writeAndFlush(encryptRpc(newSessionCreated, getMessageSeqNo(true), generateMessageId(false)));
    }

    private void processRPC(ChannelHandlerContext ctx, TLObject rpc, long messageId) {
        if (rpc != null) {
            System.out.println("HTTP TLObject:" + rpc.toString());
        } else {
            System.out.println("HTTP null rpc");
        }

        if (tlContext.isAuthorized()) {
            if (Router.getInstance().getActiveSession(tlContext.getSessionId()) == null) {
                ActiveSession session = new ActiveSession();
                session.auth_key_id = tlContext.getAuthKeyId();
                session.session_id = tlContext.getSessionId();
                session.phone = tlContext.getPhone();
                session.server = ServerConfig.SERVER_HOSTNAME;
                session.user_id = tlContext.getUserId();
                session.username = "";
                session.layer = tlContext.getApiLayer();
                session.http = true;
                Router.getInstance().addActiveSession(session);

                Router.getInstance().addChannelHandler(tlContext.getSessionId(), ctx);
            }
        }

        /*if (!(rpc instanceof Ping) && !(rpc instanceof ping_delay_disconnect) && !(rpc instanceof msgs_ack) && !(rpc instanceof TLMethod)) {
            TLVector<Long> msg_ids = new TLVector<>();
            msg_ids.add(messageId);
            msgs_ack ack = new msgs_ack(msg_ids);

            ctx.writeAndFlush(encryptRpc(ack, getMessageSeqNo(false), generateMessageId(false)));
        }*/

        if (rpc instanceof msgs_ack) {
            /*msgs_ack ack = (msgs_ack) rpc;
            for (Long msg_id : ack.msg_ids) {
                System.out.println(msg_id);
            }*/
        }

        if (rpc instanceof InvokeWithLayer) {
            tlContext.updateApiLayer(((InvokeWithLayer) rpc).layer);
            processRPC(ctx, ((InvokeWithLayer) rpc).query, messageId);
        } else if (rpc instanceof InitConnection) {
            processRPC(ctx, ((InitConnection) rpc).query, messageId);
        } else if (rpc instanceof msg_container) {
            for (message m : ((msg_container) rpc).messages) {
                //processRPC(ctx, m.body, m.msg_id);
            }
        } else if (rpc instanceof InvokeAfterMsg) {
            processRPC(ctx, ((InvokeAfterMsg) rpc).query, ((InvokeAfterMsg) rpc).msg_id);
        } else if (rpc instanceof gzip_packed) {
            ProtocolBuffer data = new ProtocolBuffer(((gzip_packed) rpc).getUncompressed());
            TLObject gzip_rpc = APIContext.getInstance().deserialize(data);
            processRPC(ctx, gzip_rpc, messageId);
        }

        if (rpc instanceof Ping || rpc instanceof ping_delay_disconnect) {

        }

        if (rpc instanceof TLMethod) {
            TLObject response = ((TLMethod) rpc).execute(tlContext, generateMessageId(false), messageId);
            rpc_result result = new rpc_result(messageId, response);
            if (response != null) {
                sendResponse(ctx, encryptRpc(result, getMessageSeqNo(true), generateMessageId(true)).getBytes());

                System.out.println("HTTP TLMethod: " + response.toString());

                if (rpc instanceof SignIn && response instanceof Authorization) {
                    if (((Authorization) response).user instanceof User) {
                        tlContext.setUserId(((User) ((Authorization) response).user).id);
                        tlContext.setPhone(((User) ((Authorization) response).user).phone);
                    } else if (((Authorization) response).user instanceof UserL45) {
                        tlContext.setUserId(((UserL45) ((Authorization) response).user).id);
                        tlContext.setPhone(((UserL45) ((Authorization) response).user).phone);
                    }

                    tlContext.setAuthorized(true);
                    System.out.println("HTTP SignIn");
                }
            }
        }
    }

    private ProtocolBuffer decryptRpc(TLContext context, byte[] bytes, byte[] messageKey) {
        MessageKeyData keyData = MessageKeyData.generateMessageKeyData(AuthKeyStore.getInstance().getAuthKey(context.getAuthKeyId()).auth_key, messageKey, false);
        byte[] decryptedData = CryptoUtils.AES256IGEDecrypt(bytes, keyData.aesIv, keyData.aesKey);
        ProtocolBuffer buff = new ProtocolBuffer(decryptedData);
        return buff;
    }

    private ProtocolBuffer encryptRpc(TLObject rpc, int seqNo, long messageId) {
        ProtocolBuffer messageBody = rpc.serialize();
        int messageSeqNo = seqNo;

        int len = 8 + 8 + 8 + 4 + 4 + messageBody.length();
        int extraLen = 0;
        while ((len + extraLen) % 16 != 0) {
            extraLen++;
        }

        ProtocolBuffer buffer = new ProtocolBuffer(len + extraLen);
        buffer.writeLong(ServerSaltStore.getInstance().getServerSalt(tlContext.getAuthKeyId()));
        buffer.writeLong(tlContext.getSessionId());
        buffer.writeLong(messageId);
        buffer.writeInt(messageSeqNo);
        buffer.writeInt(messageBody.length());
        buffer.write(messageBody.getBytes());


        byte[] messageKeyFull = buffer.getSHA1();
        byte[] messageKey = new byte[16];
        System.arraycopy(messageKeyFull, messageKeyFull.length - 16, messageKey, 0, 16);
        MessageKeyData keyData = MessageKeyData.generateMessageKeyData(AuthKeyStore.getInstance().getAuthKey(tlContext.getAuthKeyId()).auth_key, messageKey, true);

        byte[] b = new byte[extraLen];
        Utilities.random.nextBytes(b);
        buffer.write(b);

        byte[] dataForEncryption = buffer.getBytes();

        byte[] encryptedData = CryptoUtils.AES256IGEEncrypt(dataForEncryption, keyData.aesIv, keyData.aesKey);

        ProtocolBuffer data = new ProtocolBuffer(8 + messageKey.length + encryptedData.length);
        data.writeLong(tlContext.getAuthKeyId());
        data.write(messageKey);
        data.write(encryptedData);
        return data;
    }

    public int getMessageSeqNo(boolean increment) {
        int value = seq_no;
        if (increment) {
            seq_no++;
        }
        return value * 2 + (increment ? 1 : 0);
    }

    public long generateMessageId(boolean rpc_response) {
        long messageId = (long) ((((double) System.currentTimeMillis()) * 4294967296.0) / 1000.0);
        if (messageId <= lastOutgoingMessageId) {
            messageId = lastOutgoingMessageId + 1;
        }
        if (rpc_response) {
            while (messageId % 4 != 1) {
                messageId++;
            }
        } else {
            while (messageId % 4 != 3) {
                messageId++;
            }
        }

        lastOutgoingMessageId = messageId;
        return messageId;
    }

    private void sendResponse(ChannelHandlerContext ctx, byte[] response) {
        HttpResponse responseHeaders = new DefaultHttpResponse(HttpVersion.HTTP_1_1, HttpResponseStatus.OK);

        ByteBuf responseContent = Unpooled.copiedBuffer(response);

        responseHeaders.headers().set(HttpHeaderNames.CONTENT_LENGTH, responseContent.readableBytes());
        responseHeaders.headers().set(HttpHeaderNames.CONNECTION, HttpHeaderValues.KEEP_ALIVE);

        // write response
        ctx.write(responseHeaders);
        ctx.write(responseContent);
        ctx.writeAndFlush(LastHttpContent.EMPTY_LAST_CONTENT);
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
