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

package org.telegram.mtproto;

import org.telegram.tl.APIContext;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;

import java.io.Serializable;

/**
 * Created by aykut on 21/09/15.
 */
public class ProtocolBuffer implements Serializable {
    private byte[] _bytes;
    private int _readerIndex = 0;
    private int _writerIndex = 0;
    private int _limit = 0;

    /**
     *
     * @param initialCapacity
     */
    public ProtocolBuffer(int initialCapacity){
        _bytes = new byte[initialCapacity];
    }

    /**
     *
     * @param rawBytes
     */
    public ProtocolBuffer(byte[] rawBytes){
        _bytes = rawBytes;
        _limit = rawBytes.length;
    }

    /**
     *
     * @param value
     */
    public void writeByte(byte value) {
        expandIfNecessary(1);
        _bytes[_writerIndex] = value;
        _writerIndex ++;
        set_limit();
    }

    /**
     *
     * @param value
     */
    public void writeByte(int value) {
        expandIfNecessary(1);
        _bytes[_writerIndex] = (byte)value;
        _writerIndex ++;
        set_limit();
    }

    /**
     *
     * @param value
     */
    public void writeInt(int value) {
        expandIfNecessary(4);
        _bytes[_writerIndex] = (byte) (value & 0xff);
        _bytes[_writerIndex + 1] = (byte) ((value >> 8) & 0xff);
        _bytes[_writerIndex + 2] = (byte) ((value >> 16) & 0xff);
        _bytes[_writerIndex + 3] = (byte) ((value >> 24) & 0xff);

        _writerIndex += 4;
        set_limit();
    }

    /**
     *
     * @param value
     */
    public void writeLong(long value) {
        expandIfNecessary(8);
        _bytes[_writerIndex] = (byte) (value & 0xff);
        _bytes[_writerIndex + 1] = (byte) ((value >> 8) & 0xff);
        _bytes[_writerIndex + 2] = (byte) ((value >> 16) & 0xff);
        _bytes[_writerIndex + 3] = (byte) ((value >> 24) & 0xff);
        _bytes[_writerIndex + 4] = (byte) ((value >> 32) & 0xff);
        _bytes[_writerIndex + 5] = (byte) ((value >> 40) & 0xff);
        _bytes[_writerIndex + 6] = (byte) ((value >> 48) & 0xff);
        _bytes[_writerIndex + 7] = (byte) ((value >> 56) & 0xff);

        _writerIndex += 8;
        set_limit();
    }

    /**
     *
     * @param value
     */
    public void writeDouble(double value) {
        writeLong(Double.doubleToRawLongBits(value));
    }

    /**
     *
     * @param value
     */
    public void writeBool(boolean value) {
        if (value) {
            writeInt(0x997275b5);
        } else {
            writeInt(0xbc799737);
        }
    }

    /**
     *
     * @param value
     */
    public void writeString(String value) {
        writeBytes(value.getBytes());
    }

    /**
     *
     * @param value
     */
    public void writeTLObject(TLObject value) {
        write(value.serialize().getBytes());
    }

    /**
     *
     * @param value
     */
    public void writeByteBuffer(ProtocolBuffer value) {
        writeBytes(value.getBytes());
    }

    /**
     *
     * @param value
     */
    public void writeBytes(byte[] value, int offset, int count) {
        int lenBytes = 1;
        if (count >= 254) {
            lenBytes = 4;
            writeByte(254);
            writeByte(count & 0xff);
            writeByte((count >> 8) & 0xff);
            writeByte((count >> 16) & 0xff);
        } else {
            writeByte(count);
        }

        write(value, offset, count);

        if ((count + lenBytes) % 4 != 0) {
            write(new byte[4 - ((count + lenBytes) % 4)]);
        }
    }

    /**
     *
     * @param value
     */
    public void writeBytes(byte[] value) {
        int lenBytes = 1;
        if (value.length >= 254) {
            lenBytes = 4;
            writeByte(254);
            writeByte(value.length & 0xff);
            writeByte((value.length >> 8) & 0xff);
            writeByte((value.length >> 16) & 0xff);
        } else {
            writeByte(value.length);
        }

        write(value);

        if ((value.length + lenBytes) % 4 != 0) {
            write(new byte[4 - ((value.length + lenBytes) % 4)]);
        }
    }

    public void write(ProtocolBuffer value) {
        write(value.getBytes());
    }

    /**
     *
     * @param arr
     * @param offset
     * @param count
     */
    public void write(byte[] arr, int offset, int count) {
        expandIfNecessary(arr.length);

        System.arraycopy(arr, offset, _bytes, _writerIndex, count);
        _writerIndex += count;
        set_limit();
    }

    /**
     *
     * @param arr
     */
    public void write(byte[] arr) {
        expandIfNecessary(arr.length);

        System.arraycopy(arr, 0, _bytes, _writerIndex, arr.length);
        _writerIndex += arr.length;
        set_limit();
    }

    private void expandIfNecessary(int length) {
        if (_bytes == null) {
            _bytes = new byte[length];
            _writerIndex = 0;
        } else if ((_bytes.length - _writerIndex) < length) {
            byte[] tmp = new byte[_bytes.length + length];
            System.arraycopy(_bytes, 0, tmp, 0, _bytes.length);
            _bytes = tmp;
        }
    }

    public void skipBytes(int count) {
        _readerIndex += count;
    }

    public byte readByte() throws IndexOutOfBoundsException{
        if(remaining() < 1){
            throw new IndexOutOfBoundsException("There is no bytes remaining in the buffer");
        }
        byte result = _bytes[_readerIndex];
        _readerIndex++;
        return result;
    }

    public int readInt() throws IndexOutOfBoundsException{
        if(remaining() < 4){
            throw new IndexOutOfBoundsException("There is no bytes remaining in the buffer");
        }
        int result = (_bytes[_readerIndex + 3] & 0xff) << 24 |
                     (_bytes[_readerIndex + 2] & 0xff) << 16 |
                     (_bytes[_readerIndex + 1] & 0xff) << 8 |
                     (_bytes[_readerIndex] & 0xff);
        _readerIndex += 4;
        return  result;
    }

    public long readLong() throws IndexOutOfBoundsException{
        if(remaining() < 8){
            throw new IndexOutOfBoundsException("There is no bytes remaining in the buffer");
        }
        long result = ((((long)_bytes[_readerIndex + 7]) << 56) |
                (((long)_bytes[_readerIndex + 6] & 0xff) << 48) |
                (((long)_bytes[_readerIndex + 5] & 0xff) << 40) |
                (((long)_bytes[_readerIndex + 4] & 0xff) << 32) |
                (((long)_bytes[_readerIndex + 3] & 0xff) << 24) |
                (((long)_bytes[_readerIndex + 2] & 0xff) << 16) |
                (((long)_bytes[_readerIndex + 1] & 0xff) <<  8) |
                (((long)_bytes[_readerIndex] & 0xff)));
        _readerIndex += 8;
        return  result;
    }

    public double readDouble() throws IndexOutOfBoundsException{
        return Double.longBitsToDouble(readLong());
    }

    public boolean readBool(){
        try{
            int value = readInt();
            if (value == 0x997275b5) {
                return true;
            } else if (value == 0xbc799737) {
                return false;
            }
        } catch (Exception e){

        }
        return false;
    }

    public byte[] readBytes() {
        int extra = 1;
        int len = getIntFromByte(readByte());
        if(len >= 254) {
            len = getIntFromByte(readByte()) | (getIntFromByte(readByte()) << 8) | (getIntFromByte(readByte()) << 16);
            extra = 4;
        }

        byte[] raw = read(len);

        if ((len + extra) % 4 != 0) {
            skipBytes(4 - ((len + extra) % 4));
        }

        return raw;
    }

    public String readString() throws IndexOutOfBoundsException{
        return new String(readBytes());
    }

    public TLObject readTLObject(APIContext context){
        return context.deserialize(this);
    }

    public TLObject readTLVector(APIContext context, Class c) {
        this.readInt();//read constructor
        TLVector tlVector = new TLVector();
        tlVector.setDestClass(c);
        tlVector.deserialize(this);
        return tlVector;
    }

    public TLObject readBareTLType(APIContext context, TLObject bareTypeObject) {
        bareTypeObject.deserialize(this);
        return bareTypeObject;
    }


    /**
     *
     * @param length
     * @return
     */
    public byte[] read(int length) {
        if (_bytes != null && length <= remaining()) {
            byte[] tmp = new byte[length];
            System.arraycopy(_bytes, _readerIndex, tmp, 0, length);
            _readerIndex += length;
            return tmp;
        } else {
            return null;
        }
    }

    public int getIntFromByte(byte b) {
        return b >= 0 ? b : ((int)b) + 256;
    }

    /**
     *
     * @return
     */
    public byte[] getBytes(){
        byte[] tmp = new byte[_limit];
        System.arraycopy(_bytes, 0, tmp, 0, _limit);
        return tmp;
    }

    public byte[] getSHA1() {
        return Utilities.computeSHA1(_bytes, 0, _limit);
    }

    private void set_limit(){
        _limit = Math.max(_writerIndex, _limit);
    }

    public int resetReaderIndex(){
        return _readerIndex = 0;
    }


    /**
     * Gets the length of the buffer
     * @return
     */
    public int remaining(){
        return _limit - _readerIndex;
    }

    /**
     * Gets the length of the buffer
     * @return
     */
    public int length(){
        return _limit;
    }
}
