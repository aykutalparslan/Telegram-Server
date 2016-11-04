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

package org.telegram.tl.messages;

import org.telegram.core.*;
import org.telegram.data.UserModel;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;
import org.telegram.tl.service.rpc_error;

public class ReadHistory extends TLObject implements TLMethod {

    public static final int ID = 0xb04f2510;

    public TLInputPeer peer;
    public int max_id;
    public int offset;

    public ReadHistory() {
    }

    public ReadHistory(TLInputPeer peer, int max_id, int offset, boolean read_contents){
        this.peer = peer;
        this.max_id = max_id;
        this.offset = offset;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        max_id = buffer.readInt();
        offset = buffer.readInt();
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
        buff.writeTLObject(peer);
        buff.writeInt(max_id);
        buff.writeInt(offset);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        int date = (int) (System.currentTimeMillis() / 1000L);
        if (!context.isAuthorized()) {
            return new rpc_error(401, "UNAUTHORIZED");
        }
        UserModel um = UserStore.getInstance().getUser(context.getUserId());
        if (peer instanceof InputPeerUser) {
            UserModel umc = UserStore.getInstance().getUser(((InputPeerUser) peer).user_id);
            if (um != null && umc != null) {
                Object[] sessions = Router.getInstance().getActiveSessions(umc.user_id);
                for (Object session : sessions) {
                    if (((ActiveSession) session).layer >= 48) {
                        UpdateShort update = new UpdateShort(new UpdateReadHistoryOutbox(um.toPeerUser(),
                                umc.received_messages + umc.sent_messages + 1, umc.pts, 0), date);
                        Router.getInstance().Route(((ActiveSession) session).session_id,
                                ((ActiveSession) session).auth_key_id, update, false);
                    } else {
                        UpdateShort update = new UpdateShort(new UpdateReadHistoryOutbox(um.toPeerUser(),
                                umc.received_messages + umc.sent_messages + 1, umc.pts, 0), date);
                        Router.getInstance().Route(((ActiveSession) session).session_id,
                                ((ActiveSession) session).auth_key_id, update, false);
                    }
                }

                if (context.getApiLayer() >= 48) {

                    return new AffectedHistoryL48(um.pts, 0, um.pts);
                } else {

                    return new AffectedHistory(um.pts, um.pts, 0);
                }
            }
        } else if (peer instanceof InputPeerChat) {
            if (context.getApiLayer() >= 48) {

                return new AffectedHistoryL48(um.pts, 0, um.pts);
            } else {

                return new AffectedHistory(um.pts, um.pts, 0);
            }
        }
        return new rpc_error(401, "UNAUTHORIZED");
    }
}