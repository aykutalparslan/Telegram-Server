package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputGameShortName extends org.telegram.tl.TLInputGame {

    public static final int ID = 0xc331e80a;

    public org.telegram.tl.TLInputUser bot_id;
    public String short_name;

    public InputGameShortName() {
    }

    public InputGameShortName(org.telegram.tl.TLInputUser bot_id, String short_name) {
        this.bot_id = bot_id;
        this.short_name = short_name;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        bot_id = (org.telegram.tl.TLInputUser) buffer.readTLObject(APIContext.getInstance());
        short_name = buffer.readString();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(20);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeTLObject(bot_id);
        buff.writeString(short_name);
    }


    public int getConstructor() {
        return ID;
    }
}