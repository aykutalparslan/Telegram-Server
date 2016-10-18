package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ChatFull extends TLChatFull {

    public static final int ID = 0x2e02a614;

    public int id;
    public TLChatParticipants participants;
    public TLPhoto chat_photo;
    public TLPeerNotifySettings notify_settings;
    public TLExportedChatInvite exported_invite;
    public TLVector<TLBotInfo> bot_info;

    public ChatFull() {
        this.bot_info = new TLVector<>();
    }

    public ChatFull(int id, TLChatParticipants participants, TLPhoto chat_photo, TLPeerNotifySettings notify_settings, TLExportedChatInvite exported_invite, TLVector<TLBotInfo> bot_info) {
        this.id = id;
        this.participants = participants;
        this.chat_photo = chat_photo;
        this.notify_settings = notify_settings;
        this.exported_invite = exported_invite;
        this.bot_info = bot_info;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readInt();
        participants = (TLChatParticipants) buffer.readTLObject(APIContext.getInstance());
        chat_photo = (TLPhoto) buffer.readTLObject(APIContext.getInstance());
        notify_settings = (TLPeerNotifySettings) buffer.readTLObject(APIContext.getInstance());
        exported_invite = (TLExportedChatInvite) buffer.readTLObject(APIContext.getInstance());
        bot_info = (TLVector<TLBotInfo>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeInt(id);
        buff.writeTLObject(participants);
        buff.writeTLObject(chat_photo);
        buff.writeTLObject(notify_settings);
        buff.writeTLObject(exported_invite);
        buff.writeTLObject(bot_info);
    }


    public int getConstructor() {
        return ID;
    }
}