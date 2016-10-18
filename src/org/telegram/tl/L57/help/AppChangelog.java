package org.telegram.tl.L57.help;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class AppChangelog extends TLAppChangelog {

    public static final int ID = 0x4668e6bd;

    public String text;

    public AppChangelog() {
    }

    public AppChangelog(String text) {
        this.text = text;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        text = buffer.readString();
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
        buff.writeString(text);
    }


    public int getConstructor() {
        return ID;
    }
}