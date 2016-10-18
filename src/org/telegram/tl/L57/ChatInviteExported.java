package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ChatInviteExported extends TLExportedChatInvite {

    public static final int ID = 0xfc2e05bc;

    public String link;

    public ChatInviteExported() {
    }

    public ChatInviteExported(String link) {
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