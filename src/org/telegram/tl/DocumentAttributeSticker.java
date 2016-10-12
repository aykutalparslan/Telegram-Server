package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class DocumentAttributeSticker extends TLDocumentAttribute {

    public static final int ID = 0x6319d612;

    public int flags;
    public String alt;
    public TLInputStickerSet stickerset;
    public TLMaskCoords mask_coords;

    public DocumentAttributeSticker(){
    }

    public DocumentAttributeSticker(int flags, String alt, TLInputStickerSet stickerset, TLMaskCoords mask_coords) {
        this.flags = flags;
        this.alt = alt;
        this.stickerset = stickerset;
        this.mask_coords = mask_coords;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        alt = buffer.readString();
        stickerset = (TLInputStickerSet) buffer.readTLObject(APIContext.getInstance());
        if ((flags & (1 << 0)) != 0) {
            mask_coords = (TLMaskCoords) buffer.readTLObject(APIContext.getInstance());
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(32);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (mask_coords != null) {
            flags |= (1 << 0);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeString(alt);
        buff.writeTLObject(stickerset);
        if ((flags & (1 << 0)) != 0) {
            buff.writeTLObject(mask_coords);
        }
    }

    public boolean is_documentAttributeSticker_mask() {
        return (flags & (1 << 1)) != 0;
    }

    public boolean set_documentAttributeSticker_mask() {
        return (flags |= (1 << 1)) != 0;
    }

    public int getConstructor() {
        return ID;
    }
}