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

public class InputPhotoCrop extends TLInputPhotoCrop {

    public static final int ID = 0xd9915325;

    public double crop_left;
    public double crop_top;
    public double crop_width;

    public InputPhotoCrop() {
    }

    public InputPhotoCrop(double crop_left, double crop_top, double crop_width){
        this.crop_left = crop_left;
        this.crop_top = crop_top;
        this.crop_width = crop_width;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        crop_left = buffer.readDouble();
        crop_top = buffer.readDouble();
        crop_width = buffer.readDouble();
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
        buff.writeDouble(crop_left);
        buff.writeDouble(crop_top);
        buff.writeDouble(crop_width);
    }

    public int getConstructor() {
        return ID;
    }
}