package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateChannelMessageViews extends TLUpdate {

    public static final int ID = 0x98a12b4b;

    public int channel_id;
    public int id;
    public int views;

    public UpdateChannelMessageViews() {
    }

    public UpdateChannelMessageViews(int channel_id, int id, int views) {
        this.channel_id = channel_id;
        this.id = id;
        this.views = views;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        channel_id = buffer.readInt();
        id = buffer.readInt();
        views = buffer.readInt();
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
        buff.writeInt(channel_id);
        buff.writeInt(id);
        buff.writeInt(views);
    }


    public int getConstructor() {
        return ID;
    }
}