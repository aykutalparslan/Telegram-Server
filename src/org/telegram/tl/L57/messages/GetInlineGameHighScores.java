package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class GetInlineGameHighScores extends TLObject {

    public static final int ID = 0xf635e1b;

    public TLInputBotInlineMessageID id;
    public TLInputUser user_id;

    public GetInlineGameHighScores() {
    }

    public GetInlineGameHighScores(TLInputBotInlineMessageID id, TLInputUser user_id) {
        this.id = id;
        this.user_id = user_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = (TLInputBotInlineMessageID) buffer.readTLObject(APIContext.getInstance());
        user_id = (TLInputUser) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(id);
        buff.writeTLObject(user_id);
    }


    public int getConstructor() {
        return ID;
    }
}