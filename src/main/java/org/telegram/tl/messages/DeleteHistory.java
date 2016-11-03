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

import org.telegram.core.TLContext;
import org.telegram.core.TLMethod;
import org.telegram.data.DatabaseConnection;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;
import org.telegram.tl.service.rpc_error;

public class DeleteHistory extends TLObject implements TLMethod {

    public static final int ID = -185009311;

    public TLInputPeer peer;
    public int offset;

    public DeleteHistory() {
    }

    public DeleteHistory(TLInputPeer peer, int offset){
        this.peer = peer;
        this.offset = offset;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeInt(offset);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        if (context.isAuthorized()) {
            if (peer instanceof InputPeerUser) {
                DatabaseConnection.getInstance().deleteHistory(context.getUserId(), ((InputPeerUser) peer).user_id);
            }
            if (peer instanceof InputPeerContact) {
                DatabaseConnection.getInstance().deleteHistory(context.getUserId(), ((InputPeerContact) peer).user_id);
            }
            if (peer instanceof InputPeerForeign) {
                DatabaseConnection.getInstance().deleteHistory(context.getUserId(), ((InputPeerForeign) peer).user_id);
            }

            return new AffectedHistory(0, 0, 0);
        }
        return rpc_error.UNAUTHORIZED();
    }
}