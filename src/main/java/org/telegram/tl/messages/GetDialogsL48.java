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

import java.util.Collections;
import java.util.Comparator;

public class GetDialogsL48 extends TLObject implements TLMethod {

    public static final int ID = 0x6b47f94d;

    public int offset_date;
    public int offset_id;
    public TLInputPeer offset_peer;
    public int limit;

    public GetDialogsL48() {
    }

    public GetDialogsL48(int offset_date, int offset_id, TLInputPeer offset_peer, int limit) {
        this.offset_date = offset_date;
        this.offset_id = offset_id;
        this.offset_peer = offset_peer;
        this.limit = limit;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        offset_date = buffer.readInt();
        offset_id = buffer.readInt();
        offset_peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeInt(offset_date);
        buff.writeInt(offset_id);
        buff.writeTLObject(offset_peer);
        buff.writeInt(limit);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {//TODO: use offset, max_id and limit parameters
        if (context.isAuthorized()) {
            if (context.getApiLayer() >= 57) {
                return executeL57(context);
            } else {
                return executeL48(context);
            }
        }
        return new rpc_error(401, "UNAUTHORIZED");
    }

    private TLObject executeL57(TLContext context) {
        Message[] messages_in_old = DatabaseConnection.getInstance().getIncomingMessages(context.getUserId());
        Message[] messages_out_old = DatabaseConnection.getInstance().getOutgoingMessages(context.getUserId());

        org.telegram.tl.L57.Message[] messages_in = new org.telegram.tl.L57.Message[messages_in_old.length];
        for (int i = 0; i < messages_in.length; i++) {
            messages_in[i] = new org.telegram.tl.L57.Message(0, messages_in_old[i].id, messages_in_old[i].from_id, messages_in_old[i].to_id, null,
                    0, 0, messages_in_old[i].date, messages_in_old[i].message, null, null,
                    null, 0, 0);
        }

        org.telegram.tl.L57.Message[] messages_out = new org.telegram.tl.L57.Message[messages_out_old.length];
        for (int i = 0; i < messages_out.length; i++) {
            messages_out[i] = new org.telegram.tl.L57.Message(0, messages_out_old[i].id, messages_out_old[i].from_id, messages_out_old[i].to_id, null,
                    0, 0, messages_out_old[i].date, messages_out_old[i].message, null, null,
                    null, 0, 0);
        }

        TLVector<TLDialog> tlDialogs = new TLVector<>();
        TLVector<TLMessage> tlMessages = new TLVector<>();
        TLVector<TLChat> tlChats = new TLVector<>();
        TLVector<TLUser> tlUsers = new TLVector<>();
        UserModel um = UserStore.getInstance().getUser(context.getUserId());
        if (um != null) {
            tlUsers.add(um.toUser(context.getApiLayer()));
        }

        for (org.telegram.tl.L57.Message m : messages_in) {
            m.flags = 0;
            tlMessages.add(m);

            boolean dialog_exists = false;
            for (TLDialog d : tlDialogs) {
                int uid_d = 0;
                int uid_m = 0;
                if (((org.telegram.tl.L57.Dialog) d).peer instanceof PeerUser) {
                    uid_d = ((PeerUser) ((org.telegram.tl.L57.Dialog) d).peer).user_id;
                } else if (((org.telegram.tl.L57.Dialog) d).peer instanceof PeerChat) {
                    uid_d = ((PeerChat) ((org.telegram.tl.L57.Dialog) d).peer).chat_id;
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
                    org.telegram.tl.L57.Dialog d = new org.telegram.tl.L57.Dialog(0, pc, m.id, m.id, 0, 0, new PeerNotifySettingsEmpty(), 0, null);
                    tlDialogs.add(d);
                    TLChat c = ((Chat) ChatStore.getInstance().getChat(pc.chat_id)).toChatL42();
                    if (c != null) {
                        tlChats.add(c);
                    }
                } else if (m.to_id instanceof PeerUser) {
                    PeerUser pu = new PeerUser(m.from_id);
                    org.telegram.tl.L57.Dialog d = new org.telegram.tl.L57.Dialog(0, pu, m.id, m.id, 0, 0, new PeerNotifySettingsEmpty(), 0, null);
                    tlDialogs.add(d);
                    UserModel uc = UserStore.getInstance().getUser(pu.user_id);
                    if (uc != null) {
                        tlUsers.add(uc.toUser(context.getApiLayer()));
                    }
                }
            } else {
                for (TLDialog d : tlDialogs) {
                    if (((org.telegram.tl.L57.Dialog) d).peer instanceof PeerUser) {
                        if (((PeerUser) ((org.telegram.tl.L57.Dialog) d).peer).user_id == m.from_id) {
                            if (((PeerUser) ((org.telegram.tl.L57.Dialog) d).peer).user_id == m.from_id) {
                                if (((org.telegram.tl.L57.Dialog) d).read_inbox_max_id < m.id) {
                                    ((org.telegram.tl.L57.Dialog) d).read_inbox_max_id = m.id;
                                }
                                if (((org.telegram.tl.L57.Dialog) d).top_message < m.id) {
                                    ((org.telegram.tl.L57.Dialog) d).top_message = m.id;
                                }
                            }
                        }
                    }

                }
            }
        }
        for (org.telegram.tl.L57.Message m : messages_out) {
            m.set_out(true);
            tlMessages.add(m);

            boolean dialog_exists = false;
            for (TLDialog d : tlDialogs) {
                int uid_d = 0;
                int uid_m = 0;
                if (((org.telegram.tl.L57.Dialog) d).peer instanceof PeerUser) {
                    uid_d = ((PeerUser) ((org.telegram.tl.L57.Dialog) d).peer).user_id;
                } else if (((org.telegram.tl.L57.Dialog) d).peer instanceof PeerChat) {
                    uid_d = ((PeerChat) ((org.telegram.tl.L57.Dialog) d).peer).chat_id;
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
                    org.telegram.tl.L57.Dialog d = new org.telegram.tl.L57.Dialog(0, pc, m.id, m.id, 0, 0, new PeerNotifySettingsEmpty(), 0, null);
                    tlDialogs.add(d);
                    ChatL42 c = ((Chat) ChatStore.getInstance().getChat(pc.chat_id)).toChatL42();
                    if (c != null) {
                        tlChats.add(c);
                    }
                } else if (m.to_id instanceof PeerUser) {
                    PeerUser pu = (PeerUser) m.to_id;
                    org.telegram.tl.L57.Dialog d = new org.telegram.tl.L57.Dialog(0, pu, m.id, m.id, 0, 0, new PeerNotifySettingsEmpty(), 0, null);
                    tlDialogs.add(d);
                    UserModel uc = UserStore.getInstance().getUser(pu.user_id);
                    if (uc != null) {
                        tlUsers.add(uc.toUser(context.getApiLayer()));
                    }
                }

            } else {
                for (TLDialog d : tlDialogs) {
                    if (((org.telegram.tl.L57.Dialog) d).peer instanceof PeerChat && m.to_id instanceof PeerChat) {
                        if (((PeerChat) ((org.telegram.tl.L57.Dialog) d).peer).chat_id == ((PeerChat) m.to_id).chat_id) {
                            if (((org.telegram.tl.L57.Dialog) d).top_message < m.id) {
                                ((org.telegram.tl.L57.Dialog) d).top_message = m.id;
                            }
                        }
                    } else if (((org.telegram.tl.L57.Dialog) d).peer instanceof PeerUser && m.to_id instanceof PeerUser) {
                        if (((PeerUser) ((org.telegram.tl.L57.Dialog) d).peer).user_id == ((PeerUser) m.to_id).user_id) {
                            if (((org.telegram.tl.L57.Dialog) d).top_message < m.id) {
                                ((org.telegram.tl.L57.Dialog) d).top_message = m.id;
                            }
                        }
                    }
                }
            }
        }

        Collections.sort(tlMessages, new Comparator<TLMessage>() {
            @Override
            public int compare(TLMessage o1, TLMessage o2) {
                return ((org.telegram.tl.L57.Message) o2).id - ((org.telegram.tl.L57.Message) o1).id;
            }
        });

        return new org.telegram.tl.L57.messages.Dialogs(tlDialogs, tlMessages, tlChats, tlUsers);
    }

    private TLObject executeL48(TLContext context) {
        Message[] messages_in_old = DatabaseConnection.getInstance().getIncomingMessages(context.getUserId());
        Message[] messages_out_old = DatabaseConnection.getInstance().getOutgoingMessages(context.getUserId());

        MessageL48[] messages_in = new MessageL48[messages_in_old.length];
        for (int i = 0; i < messages_in.length; i++) {
            messages_in[i] = new MessageL48(0, messages_in_old[i].id, messages_in_old[i].from_id, messages_in_old[i].to_id, null,
                    0, 0, messages_in_old[i].date, messages_in_old[i].message, null, null,
                    null, 0, 0);
        }

        MessageL48[] messages_out = new MessageL48[messages_out_old.length];
        for (int i = 0; i < messages_out.length; i++) {
            messages_out[i] = new MessageL48(0, messages_out_old[i].id, messages_out_old[i].from_id, messages_out_old[i].to_id, null,
                    0, 0, messages_out_old[i].date, messages_out_old[i].message, null, null,
                    null, 0, 0);
        }

        TLVector<TLDialog> tlDialogs = new TLVector<>();
        TLVector<TLMessage> tlMessages = new TLVector<>();
        TLVector<TLChat> tlChats = new TLVector<>();
        TLVector<TLUser> tlUsers = new TLVector<>();
        UserModel um = UserStore.getInstance().getUser(context.getUserId());
        if (um != null) {
            tlUsers.add(um.toUser(context.getApiLayer()));
        }

        for (MessageL48 m : messages_in) {
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
                    ChatL42 c = ((Chat) ChatStore.getInstance().getChat(pc.chat_id)).toChatL42();
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
        for (MessageL48 m : messages_out) {
            m.set_messageL48_out();
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
                    ChatL42 c = ((Chat) ChatStore.getInstance().getChat(pc.chat_id)).toChatL42();
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
                return ((MessageL48) o2).id - ((MessageL48) o1).id;
            }
        });

        return new DialogsL42(tlDialogs, tlMessages, tlChats, tlUsers);
    }
}