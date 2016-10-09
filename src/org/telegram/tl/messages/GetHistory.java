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

import org.telegram.core.ChatStore;
import org.telegram.core.TLContext;
import org.telegram.core.TLMethod;
import org.telegram.core.UserStore;
import org.telegram.data.DatabaseConnection;
import org.telegram.data.UserModel;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

import java.util.Collections;
import java.util.Comparator;

public class GetHistory extends TLObject implements TLMethod {

    public static final int ID = -1834885329;

    public TLInputPeer peer;
    public int offset;
    public int max_id;
    public int limit;

    public GetHistory() {
    }

    public GetHistory(TLInputPeer peer, int offset, int max_id, int limit){
        this.peer = peer;
        this.offset = offset;
        this.max_id = max_id;
        this.limit = limit;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        offset = buffer.readInt();
        max_id = buffer.readInt();
        limit = buffer.readInt();
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
        buff.writeInt(max_id);
        buff.writeInt(limit);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        TLVector<TLMessage> tlMessages = new TLVector<>();
        TLVector<TLChat> tlChats = new TLVector<>();
        TLVector<TLUser> tlUsers = new TLVector<>();
        UserModel um = UserStore.getInstance().getUser(context.getUserId());
        if (um != null) {
            tlUsers.add(um.toUser(context.getApiLayer()));
        }
        if (context.isAuthorized()) {
            int peer_id = 0;
            if (peer instanceof InputPeerUser) {
                Message[] messages_in = DatabaseConnection.getInstance().getIncomingMessages(context.getUserId(), ((InputPeerUser) peer).user_id, max_id);
                Message[] messages_out = DatabaseConnection.getInstance().getOutgoingMessages(context.getUserId(), ((InputPeerUser) peer).user_id, max_id);
                for (Message m : messages_in) {
                    m.flags = 0;
                    processMessage(context, tlMessages, tlUsers, tlChats, m);
                }
                for (Message m : messages_out) {
                    m.flags = 2;
                    processMessage(context, tlMessages, tlUsers, tlChats, m);
                }
            } else if (peer instanceof InputPeerChat) {
                Message[] messages_in = DatabaseConnection.getInstance().getIncomingChatMessages(context.getUserId(), ((InputPeerChat) peer).chat_id, max_id);
                Message[] messages_out = DatabaseConnection.getInstance().getOutgoingChatMessages(context.getUserId(), ((InputPeerChat) peer).chat_id, max_id);
                for (Message m : messages_in) {
                    m.flags = 0;
                    processMessage(context, tlMessages, tlUsers, tlChats, m);
                }
                for (Message m : messages_out) {
                    m.flags = 2;
                    processMessage(context, tlMessages, tlUsers, tlChats, m);
                }
            }

        }

        Collections.sort(tlMessages, new Comparator<TLMessage>() {
            @Override
            public int compare(TLMessage o1, TLMessage o2) {
                return ((Message) o2).id - ((Message) o1).id;
            }
        });

        return new Messages(tlMessages, tlChats, tlUsers);
    }

    private void processMessage(TLContext context, TLVector<TLMessage> tlMessages, TLVector<TLUser> tlUsers, TLVector<TLChat> tlChats, Message m) {
        tlMessages.add(m);
        boolean user_exists_from = false;
        boolean user_exists_to = false;
        for (TLUser d : tlUsers) {
            if (d instanceof UserContact) {
                if (((UserContact) d).id == m.from_id) {
                    user_exists_from = true;
                }
                if (((UserContact) d).id == ((PeerUser) m.to_id).user_id) {
                    user_exists_to = true;
                }
            }
        }
        if (!user_exists_from) {
            UserModel uc = UserStore.getInstance().getUser(m.from_id);
            if (uc != null) {
                tlUsers.add(uc.toUser(context.getApiLayer()));
            }
        }
        if (!user_exists_to) {
            if (m.to_id instanceof PeerUser) {
                UserModel uc = UserStore.getInstance().getUser(((PeerUser) m.to_id).user_id);
                if (uc != null) {
                    tlUsers.add(uc.toUser(context.getApiLayer()));
                }
            } else if (m.to_id instanceof PeerChat) {
                TLChat c = ChatStore.getInstance().getChat(((PeerChat) m.to_id).chat_id);
                if (c != null) {
                    tlChats.add(c);
                }
            }

        }
    }
}