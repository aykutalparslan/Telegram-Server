package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class BotInfo extends TLBotInfo {

    public static final int ID = 0x9cf585d;

    public int user_id;
    public int version;
    public String share_text;
    public String description;
    public TLVector<TLBotCommand> commands;

    public BotInfo() {
        this.commands = new TLVector<>();
    }

    public BotInfo(int user_id, int version, String share_text, String description, TLVector<TLBotCommand> commands) {
        this.user_id = user_id;
        this.version = version;
        this.share_text = share_text;
        this.description = description;
        this.commands = commands;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        user_id = buffer.readInt();
        version = buffer.readInt();
        share_text = buffer.readString();
        description = buffer.readString();
        commands = (TLVector<TLBotCommand>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeInt(user_id);
        buff.writeInt(version);
        buff.writeString(share_text);
        buff.writeString(description);
        buff.writeTLObject(commands);
    }

    public int getConstructor() {
        return ID;
    }
}