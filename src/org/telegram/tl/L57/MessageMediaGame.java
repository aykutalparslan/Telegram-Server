package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class MessageMediaGame extends org.telegram.tl.TLMessageMedia {

    public static final int ID = 0xfdb19008;

    public org.telegram.tl.TLGame game;

    public MessageMediaGame() {
    }

    public MessageMediaGame(org.telegram.tl.TLGame game) {
        this.game = game;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        game = (org.telegram.tl.TLGame) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(12);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeTLObject(game);
    }


    public int getConstructor() {
        return ID;
    }
}