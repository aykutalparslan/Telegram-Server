package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class WallPaperSolid extends TLWallPaper {

    public static final int ID = 0x63117f24;

    public int id;
    public String title;
    public int bg_color;
    public int color;

    public WallPaperSolid() {
    }

    public WallPaperSolid(int id, String title, int bg_color, int color) {
        this.id = id;
        this.title = title;
        this.bg_color = bg_color;
        this.color = color;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readInt();
        title = buffer.readString();
        bg_color = buffer.readInt();
        color = buffer.readInt();
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
        buff.writeInt(id);
        buff.writeString(title);
        buff.writeInt(bg_color);
        buff.writeInt(color);
    }


    public int getConstructor() {
        return ID;
    }
}