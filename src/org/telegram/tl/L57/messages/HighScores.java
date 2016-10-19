package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class HighScores extends org.telegram.tl.messages.TLHighScores {

    public static final int ID = 0x9a3bfd99;

    public TLVector<org.telegram.tl.TLHighScore> scores;
    public TLVector<org.telegram.tl.TLUser> users;

    public HighScores() {
        this.scores = new TLVector<>();
        this.users = new TLVector<>();
    }

    public HighScores(TLVector<org.telegram.tl.TLHighScore> scores, TLVector<org.telegram.tl.TLUser> users) {
        this.scores = scores;
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        scores = (TLVector<org.telegram.tl.TLHighScore>) buffer.readTLObject(APIContext.getInstance());
        users = (TLVector<org.telegram.tl.TLUser>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(scores);
        buff.writeTLObject(users);
    }


    public int getConstructor() {
        return ID;
    }
}