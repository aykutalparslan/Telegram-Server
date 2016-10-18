package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class BotInfo extends TLBotInfo {

    public static final int ID = 0x98e81d3a;

    public int user_id;
    public String description;
    public TLVector<TLBotCommand> commands;

    public BotInfo() {
        this.commands = new TLVector<>();
    }

    public BotInfo(int user_id, String description, TLVector<TLBotCommand> commands) {
        this.user_id = user_id;
        this.description = description;
        this.commands = commands;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        user_id = buffer.readInt();
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
        buff.writeString(description);
        buff.writeTLObject(commands);
    }


    public int getConstructor() {
        return ID;
    }
}