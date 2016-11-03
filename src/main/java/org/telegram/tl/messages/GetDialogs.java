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
import org.telegram.tl.service.rpc_error;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;

public class GetDialogs extends TLObject implements TLMethod {

    public static final int ID = -321970698;

    public int offset;
    public int max_id;
    public int limit;

    public GetDialogs() {
    }

    public GetDialogs(int offset, int max_id, int limit){
        this.offset = offset;
        this.max_id = max_id;
        this.limit = limit;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
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
        buff.writeInt(offset);
        buff.writeInt(max_id);
        buff.writeInt(limit);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {//TODO: use offset, max_id and limit parameters
        if (context.isAuthorized()) {
            Message[] messages_in = DatabaseConnection.getInstance().getIncomingMessages(context.getUserId());
            Message[] messages_out = DatabaseConnection.getInstance().getOutgoingMessages(context.getUserId());

            TLVector<TLDialog> tlDialogs = new TLVector<>();
            TLVector<TLMessage> tlMessages = new TLVector<>();
            TLVector<TLChat> tlChats = new TLVector<>();
            TLVector<TLUser> tlUsers = new TLVector<>();
            UserModel um = UserStore.getInstance().getUser(context.getUserId());
            if (um != null) {
                tlUsers.add(um.toUser(context.getApiLayer()));
            }

            for (Message m : messages_in) {
                m.flags = 0;
                tlMessages.add(m);

                boolean dialog_exists = false;
                for (TLDialog d : tlDialogs) {
                    int uid_d = 0;
                    int uid_m = 0;
                    if (((Dialog) d).peer instanceof PeerUser) {
                        uid_d = ((PeerUser) ((Dialog) d).peer).user_id;
                    } else if (((Dialog) d).peer instanceof PeerChat) {
                        uid_d = ((PeerChat) ((Dialog) d).peer).chat_id;
                    }
                    if (m.to_id instanceof PeerUser) {
                        uid_m = ((PeerUser) m.to_id).user_id;
                    } else if (m.to_id instanceof PeerChat) {
                        uid_m = ((PeerChat) m.to_id).chat_id;
                    }

                    if (uid_d == m.from_id && m.flags != 2) {
                        dialog_exists = true;
                    }

                    if (uid_d == uid_m) {
                        dialog_exists = true;
                    }
                }
                if (!dialog_exists) {
                    if (m.to_id instanceof PeerChat) {
                        PeerChat pc = (PeerChat) m.to_id;
                        Dialog d = new Dialog(pc, m.id, m.id, 0, new PeerNotifySettingsEmpty());
                        tlDialogs.add(d);
                        TLChat c = ChatStore.getInstance().getChat(pc.chat_id);
                        if (c != null) {
                            tlChats.add(c);
                        }
                    } else if (m.to_id instanceof PeerUser) {
                        PeerUser pu = new PeerUser(m.from_id);
                        Dialog d = new Dialog(pu, m.id, m.id, 0, new PeerNotifySettingsEmpty());
                        tlDialogs.add(d);
                        UserModel uc = UserStore.getInstance().getUser(pu.user_id);
                        if (uc != null) {
                            tlUsers.add(uc.toUser(context.getApiLayer()));
                        }
                    }
                } else {
                    for (TLDialog d : tlDialogs) {
                        if (((Dialog) d).peer instanceof PeerUser) {
                            if (((PeerUser) ((Dialog) d).peer).user_id == m.from_id) {
                                if (((PeerUser) ((Dialog) d).peer).user_id == m.from_id) {
                                    if (((Dialog) d).read_inbox_max_id < m.id) {
                                        ((Dialog) d).read_inbox_max_id = m.id;
                                    }
                                    if (((Dialog) d).top_message < m.id) {
                                        ((Dialog) d).top_message = m.id;
                                    }
                                }
                            }
                        }

                    }
                }
            }
            for (Message m : messages_out) {
                m.flags = 2;
                tlMessages.add(m);

                boolean dialog_exists = false;
                for (TLDialog d : tlDialogs) {
                    int uid_d = 0;
                    int uid_m = 0;
                    if (((Dialog) d).peer instanceof PeerUser) {
                        uid_d = ((PeerUser) ((Dialog) d).peer).user_id;
                    } else if (((Dialog) d).peer instanceof PeerChat) {
                        uid_d = ((PeerChat) ((Dialog) d).peer).chat_id;
                    }
                    if (m.to_id instanceof PeerUser) {
                        uid_m = ((PeerUser) m.to_id).user_id;
                    } else if (m.to_id instanceof PeerChat) {
                        uid_m = ((PeerChat) m.to_id).chat_id;
                    }
                    if (uid_d == uid_m) {
                        dialog_exists = true;
                    }
                }
                if (!dialog_exists) {
                    if (m.to_id instanceof PeerChat) {
                        PeerChat pc = (PeerChat) m.to_id;
                        Dialog d = new Dialog(pc, m.id, m.id, 0, new PeerNotifySettingsEmpty());
                        tlDialogs.add(d);
                        TLChat c = ChatStore.getInstance().getChat(pc.chat_id);
                        if (c != null) {
                            tlChats.add(c);
                        }
                    } else if (m.to_id instanceof PeerUser) {
                        PeerUser pu = (PeerUser) m.to_id;
                        Dialog d = new Dialog(pu, m.id, m.id, 0, new PeerNotifySettingsEmpty());
                        tlDialogs.add(d);
                        UserModel uc = UserStore.getInstance().getUser(pu.user_id);
                        if (uc != null) {
                            tlUsers.add(uc.toUser(context.getApiLayer()));
                        }
                    }

                } else {
                    for (TLDialog d : tlDialogs) {
                        if (((Dialog) d).peer instanceof PeerChat && m.to_id instanceof PeerChat) {
                            if (((PeerChat) ((Dialog) d).peer).chat_id == ((PeerChat) m.to_id).chat_id) {
                                if (((Dialog) d).top_message < m.id) {
                                    ((Dialog) d).top_message = m.id;
                                }
                            }
                        } else if (((Dialog) d).peer instanceof PeerUser && m.to_id instanceof PeerUser) {
                            if (((PeerUser) ((Dialog) d).peer).user_id == ((PeerUser) m.to_id).user_id) {
                                if (((Dialog) d).top_message < m.id) {
                                    ((Dialog) d).top_message = m.id;
                                }
                            }
                        }
                    }
                }
            }

            Collections.sort(tlMessages, new Comparator<TLMessage>() {
                @Override
                public int compare(TLMessage o1, TLMessage o2) {
                    return ((Message) o2).id - ((Message) o1).id;
                }
            });

            return new Dialogs(tlDialogs, tlMessages, tlChats, tlUsers);
        }
        return new rpc_error(401, "UNAUTHORIZED");
    }
}