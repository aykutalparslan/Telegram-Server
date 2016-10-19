package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateWebPage extends org.telegram.tl.TLUpdate {

    public static final int ID = 0x7f891213;

    public org.telegram.tl.TLWebPage webpage;
    public int pts;
    public int pts_count;

    public UpdateWebPage() {
    }

    public UpdateWebPage(org.telegram.tl.TLWebPage webpage, int pts, int pts_count) {
        this.webpage = webpage;
        this.pts = pts;
        this.pts_count = pts_count;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        webpage = (org.telegram.tl.TLWebPage) buffer.readTLObject(APIContext.getInstance());
        pts = buffer.readInt();
        pts_count = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(20);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeTLObject(webpage);
        buff.writeInt(pts);
        buff.writeInt(pts_count);
    }


    public int getConstructor() {
        return ID;
    }
}