package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class BotCommandOld extends TLBotCommand {

    public static final int ID = 0xb79d22ab;

    public String command;
    public String params;
    public String description;

    public BotCommandOld() {
    }

    public BotCommandOld(String command, String params, String description) {
        this.command = command;
        this.params = params;
        this.description = description;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        command = buffer.readString();
        params = buffer.readString();
        description = buffer.readString();
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
        buff.writeString(command);
        buff.writeString(params);
        buff.writeString(description);
    }

    public int getConstructor() {
        return ID;
    }
}