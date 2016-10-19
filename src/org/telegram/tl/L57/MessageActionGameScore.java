package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class MessageActionGameScore extends org.telegram.tl.TLMessageAction {

    public static final int ID = 0x92a72876;

    public long game_id;
    public int score;

    public MessageActionGameScore() {
    }

    public MessageActionGameScore(long game_id, int score) {
        this.game_id = game_id;
        this.score = score;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        game_id = buffer.readLong();
        score = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(16);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeLong(game_id);
        buff.writeInt(score);
    }


    public int getConstructor() {
        return ID;
    }
}