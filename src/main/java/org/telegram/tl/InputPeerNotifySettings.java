/*
 *     This file is part of Telegram Server
 *     Copyright (C) 2015  Aykut Alparslan KOÇ
 *
 *     Telegram Server is free software: you can redistribute it and/or modify
 *     it under the terms of the GNU General Public License as published by
 *     the Free Software Foundation, either version 3 of the License, or
 *     (at your option) any later version.
 *
 *     Telegram Server is distributed in the hope that it will be useful,
 *     but WITHOUT ANY WARRANTY; without even the implied warranty of
 *     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *     GNU General Public License for more details.
 *
 *     You should have received a copy of the GNU General Public License
 *     along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class InputPeerNotifySettings extends TLInputPeerNotifySettings {

    public static final int ID = 1185074840;

    public int mute_until;
    public String sound;
    public boolean show_previews;
    public int events_mask;

    public InputPeerNotifySettings() {
    }

    public InputPeerNotifySettings(int mute_until, String sound, boolean show_previews, int events_mask){
        this.mute_until = mute_until;
        this.sound = sound;
        this.show_previews = show_previews;
        this.events_mask = events_mask;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        mute_until = buffer.readInt();
        sound = buffer.readString();
        show_previews = buffer.readBool();
        events_mask = buffer.readInt();
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
        buff.writeInt(mute_until);
        buff.writeString(sound);
        buff.writeBool(show_previews);
        buff.writeInt(events_mask);
    }

    public int getConstructor() {
        return ID;
    }
}