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

package org.telegram.tl.L57.messages;

import org.telegram.core.ChatStore;
import org.telegram.core.TLContext;
import org.telegram.core.TLMethod;
import org.telegram.core.UserStore;
import org.telegram.data.DatabaseConnection;
import org.telegram.data.UserModel;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;
import org.telegram.tl.L57.InputPeerChat;
import org.telegram.tl.L57.InputPeerUser;
import org.telegram.tl.L57.PeerChat;
import org.telegram.tl.L57.PeerUser;

import java.util.Collections;
import java.util.Comparator;

public class GetHistory extends TLObject implements TLMethod {

    public static final int ID = 0xafa92846;

    public org.telegram.tl.TLInputPeer peer;
    public int offset_id;
    public int offset_date;
    public int add_offset;
    public int limit;
    public int max_id;
    public int min_id;

    public GetHistory() {
    }

    public GetHistory(org.telegram.tl.TLInputPeer peer, int offset_id, int offset_date, int add_offset, int limit, int max_id, int min_id) {
        this.peer = peer;
        this.offset_id = offset_id;
        this.offset_date = offset_date;
        this.add_offset = add_offset;
        this.limit = limit;
        this.max_id = max_id;
        this.min_id = min_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (org.telegram.tl.TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        offset_id = buffer.readInt();
        offset_date = buffer.readInt();
        add_offset = buffer.readInt();
        limit = buffer.readInt();
        max_id = buffer.readInt();
        min_id = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(36);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeTLObject(peer);
        buff.writeInt(offset_id);
        buff.writeInt(offset_date);
        buff.writeInt(add_offset);
        buff.writeInt(limit);
        buff.writeInt(max_id);
        buff.writeInt(min_id);
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
                org.telegram.tl.Message[] messages_in_old = DatabaseConnection.getInstance().getIncomingMessages(context.getUserId(), ((InputPeerUser) peer).user_id, max_id);
                org.telegram.tl.Message[] messages_out_old = DatabaseConnection.getInstance().getOutgoingMessages(context.getUserId(), ((InputPeerUser) peer).user_id, max_id);
                org.telegram.tl.L57.Message[] messages_in = new org.telegram.tl.L57.Message[messages_in_old.length];
                for (int i = 0; i < messages_in.length; i++) {
                    messages_in[i] = new org.telegram.tl.L57.Message(messages_in[i].flags, messages_in_old[i].id, messages_in_old[i].from_id, messages_in_old[i].to_id, null,
                            0, 0, messages_in_old[i].date, messages_in_old[i].message, messages_in_old[i].media, null,
                            null, 0, 0);
                }

                org.telegram.tl.L57.Message[] messages_out = new org.telegram.tl.L57.Message[messages_out_old.length];
                for (int i = 0; i < messages_out.length; i++) {
                    messages_out[i] = new org.telegram.tl.L57.Message(messages_out[i].flags, messages_out_old[i].id, messages_out_old[i].from_id, messages_out_old[i].to_id, null,
                            0, 0, messages_out_old[i].date, messages_out_old[i].message, messages_out_old[i].media, null,
                            null, 0, 0);
                }
                for (org.telegram.tl.L57.Message m : messages_in) {
                    m.flags = 0;
                    processMessage(context, tlMessages, tlUsers, tlChats, m);
                }
                for (org.telegram.tl.L57.Message m : messages_out) {
                    m.flags = 2;
                    m.set_out(true);
                    processMessage(context, tlMessages, tlUsers, tlChats, m);
                }
            } else if (peer instanceof InputPeerChat) {
                org.telegram.tl.Message[] messages_in_old = DatabaseConnection.getInstance().getIncomingChatMessages(context.getUserId(), ((InputPeerChat) peer).chat_id, max_id);
                org.telegram.tl.Message[] messages_out_old = DatabaseConnection.getInstance().getOutgoingChatMessages(context.getUserId(), ((InputPeerChat) peer).chat_id, max_id);
                org.telegram.tl.L57.Message[] messages_in = new org.telegram.tl.L57.Message[messages_in_old.length];
                for (int i = 0; i < messages_in.length; i++) {
                    messages_in[i] = new org.telegram.tl.L57.Message(0, messages_in_old[i].id, messages_in_old[i].from_id, messages_in_old[i].to_id, null,
                            0, 0, messages_in_old[i].date, messages_in_old[i].message, messages_in_old[i].media, null,
                            null, 0, 0);
                }

                org.telegram.tl.L57.Message[] messages_out = new org.telegram.tl.L57.Message[messages_out_old.length];
                for (int i = 0; i < messages_out.length; i++) {
                    messages_out[i] = new org.telegram.tl.L57.Message(0, messages_out_old[i].id, messages_out_old[i].from_id, messages_out_old[i].to_id, null,
                            0, 0, messages_out_old[i].date, messages_out_old[i].message, messages_out_old[i].media, null,
                            null, 0, 0);
                }
                for (org.telegram.tl.L57.Message m : messages_in) {
                    m.flags = 0;
                    processMessage(context, tlMessages, tlUsers, tlChats, m);
                }
                for (org.telegram.tl.L57.Message m : messages_out) {
                    m.flags = 2;
                    m.set_out(true);
                    processMessage(context, tlMessages, tlUsers, tlChats, m);
                }
            }

        }

        Collections.sort(tlMessages, new Comparator<TLMessage>() {
            @Override
            public int compare(TLMessage o1, TLMessage o2) {
                return ((org.telegram.tl.L57.Message) o2).id - ((org.telegram.tl.L57.Message) o1).id;
            }
        });

        return new Messages(tlMessages, tlChats, tlUsers);
    }

    private void processMessage(TLContext context, TLVector<TLMessage> tlMessages, TLVector<TLUser> tlUsers, TLVector<TLChat> tlChats, org.telegram.tl.L57.Message m) {
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