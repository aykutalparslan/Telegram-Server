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

package org.telegram.api;

import com.hazelcast.core.IMap;
import com.hazelcast.query.SqlPredicate;
import io.netty.channel.ChannelHandlerContext;
import org.telegram.data.HazelcastConnection;
import org.telegram.mtproto.MessageKeyData;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.mtproto.Utilities;
import org.telegram.mtproto.secure.CryptoUtils;
import org.telegram.server.ServerConfig;
import org.telegram.tl.TLChat;
import org.telegram.tl.TLObject;

import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ConcurrentMap;

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

    public ActiveSession[] getActiveSessions(String phone) {
        return (ActiveSession[]) activeSessions.values(new SqlPredicate("phone = " + phone)).toArray();
    }

    public void addActiveSession(ActiveSession session) {
        activeSessions.set(session.session_id, session);
    }

    public void addChannelHandler(TLContext context, ChannelHandlerContext ctx) {
        channelHandlers.put(context.getSessionId(), ctx);
    }

    public ActiveSession getActiveSession(long session_id) {
        return activeSessions.get(session_id);
    }

    public void removeActiveSession(long session_id) {
        activeSessions.delete(session_id);
    }

    public void Route(TLContext context, TLObject msg, long msg_id, int seq_no) {
        ChannelHandlerContext ctx = channelHandlers.get(context.getSessionId());
        ctx.writeAndFlush(encryptRpc(msg, seq_no, msg_id, context));
    }

    private ProtocolBuffer encryptRpc(TLObject rpc, int seqNo, long messageId, TLContext context) {
        ProtocolBuffer messageBody = rpc.serialize();
        int messageSeqNo = seqNo;

        int len = 8 + 8 + 8 + 4 + 4 + messageBody.length();
        int extraLen = 0;
        while ((len + extraLen) % 16 != 0) {
            extraLen++;
        }

        ProtocolBuffer buffer = new ProtocolBuffer(len + extraLen);
        buffer.writeLong(ServerSaltStore.getInstance().getServerSalt(context.getAuthKeyId()));
        buffer.writeLong(context.getSessionId());
        buffer.writeLong(messageId);
        buffer.writeInt(messageSeqNo);
        buffer.writeInt(messageBody.length());
        buffer.write(messageBody.getBytes());

        byte[] messageKeyFull = buffer.getSHA1();
        byte[] messageKey = new byte[16];
        System.arraycopy(messageKeyFull, messageKeyFull.length - 16, messageKey, 0, 16);
        MessageKeyData keyData = MessageKeyData.generateMessageKeyData(AuthKeyStore.getInstance().getAuthKey(context.getAuthKeyId()), messageKey, true);

        byte[] b = new byte[extraLen];
        Utilities.random.nextBytes(b);
        buffer.write(b);

        byte[] dataForEncryption = buffer.getBytes();

        byte[] encryptedData = CryptoUtils.AES256IGEEncrypt(dataForEncryption, keyData.aesIv, keyData.aesKey);

        ProtocolBuffer data = new ProtocolBuffer(8 + messageKey.length + encryptedData.length);
        data.writeLong(context.getAuthKeyId());
        data.write(messageKey);
        data.write(encryptedData);

        return data;
    }
}