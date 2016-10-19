package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class WallPaper extends org.telegram.tl.TLWallPaper {

    public static final int ID = 0xccb03657;

    public int id;
    public String title;
    public TLVector<org.telegram.tl.TLPhotoSize> sizes;
    public int color;

    public WallPaper() {
        this.sizes = new TLVector<>();
    }

    public WallPaper(int id, String title, TLVector<org.telegram.tl.TLPhotoSize> sizes, int color) {
        this.id = id;
        this.title = title;
        this.sizes = sizes;
        this.color = color;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readInt();
        title = buffer.readString();
        sizes = (TLVector<org.telegram.tl.TLPhotoSize>) buffer.readTLObject(APIContext.getInstance());
        color = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(28);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(id);
        buff.writeString(title);
        buff.writeTLObject(sizes);
        buff.writeInt(color);
    }


    public int getConstructor() {
        return ID;
    }
}