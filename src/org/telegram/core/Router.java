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

package org.telegram.core;

import com.hazelcast.core.IMap;
import com.hazelcast.query.SqlPredicate;
import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;
import io.netty.channel.ChannelHandlerContext;
import io.netty.handler.codec.http.*;
import org.telegram.data.HazelcastConnection;
import org.telegram.mtproto.MessageKeyData;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.mtproto.Utilities;
import org.telegram.mtproto.secure.CryptoUtils;
import org.telegram.server.TelegramHTTPServerHandler;
import org.telegram.server.TelegramServerHandler;
import org.telegram.tl.TLObject;

import java.util.Collection;
import java.util.concurrent.ConcurrentHashMap;

/**
 * Created by aykut on 17/11/15.
 */
public class Router {
    IMap<Long, ActiveSession> activeSessions;
    ConcurrentHashMap<Long, ChannelHandlerContext> channelHandlers = new ConcurrentHashMap<>();

    private static Router _instance;

    private Router() {
        activeSessions = HazelcastConnection.getInstance().getMap("telegram_active_sessions");
        activeSessions.addIndex("phone", true);
        //activeSessions.addIndex("user_id", true);
        //activeSessions.addIndex("username", true);
    }

    public static Router getInstance() {
        if (_instance == null) {
            _instance = new Router();
        }
        return _instance;
    }

    public void addActiveSession(ActiveSession session) {
        activeSessions.set(session.session_id, session);
    }

    public void addChannelHandler(long session_id, ChannelHandlerContext ctx) {
        channelHandlers.put(session_id, ctx);
    }

    public ActiveSession getActiveSession(long session_id) {
        return activeSessions.get(session_id);
    }

    public void removeActiveSession(long session_id) {
        activeSessions.delete(session_id);
    }

    public Object[] getActiveSessions(int user_id) {
        return activeSessions.values(new SqlPredicate("user_id = " + user_id)).toArray();
    }

    public void Route(int user_id, TLObject msg, boolean rpc_response) {
        for (Object sess : activeSessions.values(new SqlPredicate("user_id = " + user_id)).toArray()) {
            //for now all sessions are on the same server
            ChannelHandlerContext ctx = channelHandlers.get(((ActiveSession) sess).session_id);
            if (ctx != null) {
                if (((ActiveSession) sess).http) {
                    long msg_id = ((TelegramHTTPServerHandler) ctx.handler()).generateMessageId(rpc_response);

                    sendResponse(ctx, encryptRpc(msg, ((TelegramHTTPServerHandler) ctx.handler()).getMessageSeqNo(true), msg_id,
                            ((ActiveSession) sess).session_id, ((ActiveSession) sess).auth_key_id).getBytes());
                } else {
                    long msg_id = ((TelegramServerHandler) ctx.handler()).generateMessageId(rpc_response);

                    ctx.writeAndFlush(encryptRpc(msg, ((TelegramServerHandler) ctx.handler()).getMessageSeqNo(true), msg_id,
                            ((ActiveSession) sess).session_id, ((ActiveSession) sess).auth_key_id));
                }
            }
        }
    }

    public void Route(long session_id, long auth_key_id, TLObject msg, boolean rpc_response) {
        ChannelHandlerContext ctx = channelHandlers.get(session_id);
        if (ctx != null) {


            ActiveSession session = activeSessions.get(session_id);
            if (session.http) {
                long msg_id = ((TelegramHTTPServerHandler) ctx.handler()).generateMessageId(rpc_response);

                sendResponse(ctx, encryptRpc(msg, ((TelegramHTTPServerHandler) ctx.handler()).getMessageSeqNo(true), msg_id,
                        session_id, auth_key_id).getBytes());
            } else {
                long msg_id = ((TelegramServerHandler) ctx.handler()).generateMessageId(rpc_response);

                ctx.writeAndFlush(encryptRpc(msg, ((TelegramServerHandler) ctx.handler()).getMessageSeqNo(true), msg_id,
                        session_id, auth_key_id));
            }

        }
    }

    private ProtocolBuffer encryptRpc(TLObject rpc, int seqNo, long messageId, long session_id, long auth_key_id) {
        ProtocolBuffer messageBody = rpc.serialize();
        int messageSeqNo = seqNo;

        int len = 8 + 8 + 8 + 4 + 4 + messageBody.length();
        int extraLen = 0;
        while ((len + extraLen) % 16 != 0) {
            extraLen++;
        }

        ProtocolBuffer buffer = new ProtocolBuffer(len + extraLen);
        buffer.writeLong(ServerSaltStore.getInstance().getServerSalt(auth_key_id));
        buffer.writeLong(session_id);
        buffer.writeLong(messageId);
        buffer.writeInt(messageSeqNo);
        buffer.writeInt(messageBody.length());
        buffer.write(messageBody.getBytes());

        byte[] messageKeyFull = buffer.getSHA1();
        byte[] messageKey = new byte[16];
        System.arraycopy(messageKeyFull, messageKeyFull.length - 16, messageKey, 0, 16);
        MessageKeyData keyData = MessageKeyData.generateMessageKeyData(AuthKeyStore.getInstance().getAuthKey(auth_key_id).auth_key, messageKey, true);

        byte[] b = new byte[extraLen];
        Utilities.random.nextBytes(b);
        buffer.write(b);

        byte[] dataForEncryption = buffer.getBytes();
        byte[] encryptedData = CryptoUtils.AES256IGEEncrypt(dataForEncryption, keyData.aesIv, keyData.aesKey);

        ProtocolBuffer data = new ProtocolBuffer(8 + messageKey.length + encryptedData.length);
        data.writeLong(auth_key_id);
        data.write(messageKey);
        data.write(encryptedData);
        return data;
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
}
