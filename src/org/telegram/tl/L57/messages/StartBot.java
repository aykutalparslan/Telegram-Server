package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class StartBot extends TLObject {

    public static final int ID = 0xe6df7378;

    public TLInputUser bot;
    public TLInputPeer peer;
    public long random_id;
    public String start_param;

    public StartBot() {
    }

    public StartBot(TLInputUser bot, TLInputPeer peer, long random_id, String start_param) {
        this.bot = bot;
        this.peer = peer;
        this.random_id = random_id;
        this.start_param = start_param;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        bot = (TLInputUser) buffer.readTLObject(APIContext.getInstance());
        peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        random_id = buffer.readLong();
        start_param = buffer.readString();
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
        buff.writeTLObject(bot);
        buff.writeTLObject(peer);
        buff.writeLong(random_id);
        buff.writeString(start_param);
    }


    public int getConstructor() {
        return ID;
    }
}