package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputBotInlineResultGame extends org.telegram.tl.TLInputBotInlineResult {

    public static final int ID = 0x4fa417f2;

    public String id;
    public String short_name;
    public org.telegram.tl.TLInputBotInlineMessage send_message;

    public InputBotInlineResultGame() {
    }

    public InputBotInlineResultGame(String id, String short_name, org.telegram.tl.TLInputBotInlineMessage send_message) {
        this.id = id;
        this.short_name = short_name;
        this.send_message = send_message;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readString();
        short_name = buffer.readString();
        send_message = (org.telegram.tl.TLInputBotInlineMessage) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(28);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeString(id);
        buff.writeString(short_name);
        buff.writeTLObject(send_message);
    }


    public int getConstructor() {
        return ID;
    }
}