package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateBotCallbackQuery extends TLUpdate {

    public static final int ID = 0xe73547e1;

    public int flags;
    public long query_id;
    public int user_id;
    public TLPeer peer;
    public int msg_id;
    public long chat_instance;
    public byte[] data;
    public String game_short_name;

    public UpdateBotCallbackQuery() {
    }

    public UpdateBotCallbackQuery(int flags, long query_id, int user_id, TLPeer peer, int msg_id, long chat_instance, byte[] data, String game_short_name) {
        this.flags = flags;
        this.query_id = query_id;
        this.user_id = user_id;
        this.peer = peer;
        this.msg_id = msg_id;
        this.chat_instance = chat_instance;
        this.data = data;
        this.game_short_name = game_short_name;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        query_id = buffer.readLong();
        user_id = buffer.readInt();
        peer = (TLPeer) buffer.readTLObject(APIContext.getInstance());
        msg_id = buffer.readInt();
        chat_instance = buffer.readLong();
        if ((flags & (1 << 0)) != 0) {
            data = buffer.readBytes();
        }
        if ((flags & (1 << 1)) != 0) {
            game_short_name = buffer.readString();
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
        if (data.length != 0) {
            flags |= (1 << 0);
        }
        if (game_short_name != null && !game_short_name.isEmpty()) {
            flags |= (1 << 1);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeLong(query_id);
        buff.writeInt(user_id);
        buff.writeTLObject(peer);
        buff.writeInt(msg_id);
        buff.writeLong(chat_instance);
        if ((flags & (1 << 0)) != 0) {
            buff.writeBytes(data);
        }
        if ((flags & (1 << 1)) != 0) {
            buff.writeString(game_short_name);
        }
    }


    public int getConstructor() {
        return ID;
    }
}