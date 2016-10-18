package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ExportedMessageLink extends TLExportedMessageLink {

    public static final int ID = 0x1f486803;

    public String link;

    public ExportedMessageLink() {
    }

    public ExportedMessageLink(String link) {
        this.link = link;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        link = buffer.readString();
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
        buff.writeString(link);
    }


    public int getConstructor() {
        return ID;
    }
}