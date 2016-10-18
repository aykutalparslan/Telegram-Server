package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class BotCommand extends TLBotCommand {

    public static final int ID = 0xc27ac8c7;

    public String command;
    public String description;

    public BotCommand() {
    }

    public BotCommand(String command, String description) {
        this.command = command;
        this.description = description;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        command = buffer.readString();
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
        buff.writeString(description);
    }


    public int getConstructor() {
        return ID;
    }
}