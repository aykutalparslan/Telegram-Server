package org.telegram.tl.L57.help;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class AppUpdate extends org.telegram.tl.help.TLAppUpdate {

    public static final int ID = 0x8987f311;

    public int id;
    public boolean critical;
    public String url;
    public String text;

    public AppUpdate() {
    }

    public AppUpdate(int id, boolean critical, String url, String text) {
        this.id = id;
        this.critical = critical;
        this.url = url;
        this.text = text;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readInt();
        critical = buffer.readBool();
        url = buffer.readString();
        text = buffer.readString();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(25);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(id);
        buff.writeBool(critical);
        buff.writeString(url);
        buff.writeString(text);
    }


    public int getConstructor() {
        return ID;
    }
}