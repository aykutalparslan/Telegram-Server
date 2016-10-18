package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class RequestEncryption extends TLObject {

    public static final int ID = 0xf64daf43;

    public TLInputUser user_id;
    public int random_id;
    public byte[] g_a;

    public RequestEncryption() {
    }

    public RequestEncryption(TLInputUser user_id, int random_id, byte[] g_a) {
        this.user_id = user_id;
        this.random_id = random_id;
        this.g_a = g_a;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        user_id = (TLInputUser) buffer.readTLObject(APIContext.getInstance());
        random_id = buffer.readInt();
        g_a = buffer.readBytes();
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
        buff.writeTLObject(user_id);
        buff.writeInt(random_id);
        buff.writeBytes(g_a);
    }


    public int getConstructor() {
        return ID;
    }
}