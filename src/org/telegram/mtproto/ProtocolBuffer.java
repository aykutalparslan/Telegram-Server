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

import io.netty.buffer.ByteBuf;
import io.netty.buffer.ByteBufAllocator;
import io.netty.buffer.PooledByteBufAllocator;
import org.telegram.tl.DeserializationContext;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;

import java.io.Serializable;
import java.security.MessageDigest;

/**
 * Created by aykut on 21/09/15.
 */
public class ProtocolBuffer implements Serializable {
    private static final ByteBufAllocator _alloc = PooledByteBufAllocator.DEFAULT;
    private ByteBuf _buffer;
    private int _limit;
    /**
     *
     * @param initialCapacity
     */
    public ProtocolBuffer(int initialCapacity){
        _buffer = _alloc.directBuffer(initialCapacity);
        _buffer.writerIndex(0);
        _limit = 0;
    }

    /**
     *
     * @param rawBytes
     */
    public ProtocolBuffer(byte[] rawBytes){
        _buffer = _alloc.directBuffer(rawBytes.length);
        _buffer.writerIndex(0);
        _buffer.writeBytes(rawBytes);
        _limit = 0;
    }

    /**
     * @param buffer
     */
    public ProtocolBuffer(ByteBuf buffer) {
        _buffer = buffer;
        _limit = 0;
    }

    /**
     *
     * @param value
     */
    public void writeByte(byte value) {
        expandIfNecessary(1);
        _buffer.writeByte(value);
        _limit++;
    }

    /**
     *
     * @param value
     */
    public void writeByte(int value) {
        expandIfNecessary(1);
        _buffer.writeByte(value);
        _limit++;
    }

    /**
     *
     * @param value
     */
    public void writeInt(int value) {
        expandIfNecessary(4);
        _buffer.writeIntLE(value);
        _limit += 4;
    }

    /**
     *
     * @param value
     */
    public void writeLong(long value) {
        expandIfNecessary(8);
        _buffer.writeLongLE(value);
        _limit += 8;
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
        if (value == null) {
            writeBytes("".getBytes());
        } else {
            writeBytes(value.getBytes());
        }
    }

    /**
     *
     * @param value
     */
    public void writeTLObject(TLObject value) {
        ProtocolBuffer buffer = value.serialize();
        write(buffer.getBytes());
        buffer.release();
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
            expandIfNecessary(value.length + 4);
            lenBytes = 4;
            writeByte(254);
            writeByte(count & 0xff);
            writeByte((count >> 8) & 0xff);
            writeByte((count >> 16) & 0xff);
        } else {
            expandIfNecessary(value.length + 1);
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
            expandIfNecessary(value.length + 4);
            lenBytes = 4;
            writeByte(254);
            writeByte(value.length & 0xff);
            writeByte((value.length >> 8) & 0xff);
            writeByte((value.length >> 16) & 0xff);
        } else {
            expandIfNecessary(value.length + 1);
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

        _buffer.writeBytes(arr, offset, count);
        _limit += count;
    }

    /**
     *
     * @param arr
     */
    public void write(byte[] arr) {
        expandIfNecessary(arr.length);

        _buffer.writeBytes(arr);
        _limit += arr.length;
    }

    private void expandIfNecessary(int length) {
        if (_buffer == null) {
            _buffer = _alloc.directBuffer(length);
        } else if (_buffer.capacity() - _buffer.writerIndex() < length) {
            _buffer.capacity(Math.max(_buffer.capacity() + length, _buffer.capacity() * 2));
        }
    }


    public byte readByte() throws IndexOutOfBoundsException{
        if (_buffer.readableBytes() < 1) {
            throw new IndexOutOfBoundsException("There is no bytes remaining in the buffer");
        }

        return read(1)[0];
        //return _buffer.readByte();
    }

    public int readInt() throws IndexOutOfBoundsException{
        if (_buffer.readableBytes() < 4) {
            throw new IndexOutOfBoundsException("There is no bytes remaining in the buffer");
        }
        return _buffer.readIntLE();
    }

    public long readLong() throws IndexOutOfBoundsException{
        if (_buffer.readableBytes() < 8) {
            throw new IndexOutOfBoundsException("There is no bytes remaining in the buffer");
        }
        return _buffer.readLongLE();
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
        if (len >= 254 || len == 0) {
            len = getIntFromByte(readByte()) | (getIntFromByte(readByte()) << 8) | (getIntFromByte(readByte()) << 16);
            extra = 4;
        }

        byte[] raw = read(len);

        if ((len + extra) % 4 != 0) {
            _buffer.readerIndex(_buffer.readerIndex() + (4 - ((len + extra) % 4)));
        }

        return raw;
    }

    public String readString() throws IndexOutOfBoundsException{
        byte[] stringBytes = readBytes();
        if (stringBytes == null) {
            return "";
        }
        return new String(stringBytes);
    }

    public TLObject readTLObject(DeserializationContext context) {
        return context.deserialize(this);
    }

    public TLObject readTLVector(Class c) {
        this.readInt();//read constructor
        TLVector tlVector = new TLVector();
        tlVector.setDestClass(c);
        tlVector.deserialize(this);
        return tlVector;
    }

    public TLObject readBareTLType(TLObject o) {
        o.deserialize(this);
        return o;
    }


    /**
     *
     * @param length
     * @return
     */
    public byte[] read(int length) {
        if (length <= 0) {
            return null;
        }
        if (_buffer != null && length <= _buffer.readableBytes()) {
            byte[] tmp = new byte[length];
            _buffer.readBytes(tmp, 0, length);
            return tmp;
        } else {
            return null;
        }
    }

    private int getIntFromByte(byte b) {
        return b >= 0 ? b : ((int)b) + 256;
    }

    /**
     *
     * @return
     */
    public byte[] getBytes(){
        byte[] tmp = new byte[_buffer.writerIndex()];
        _buffer.getBytes(0, tmp, 0, _buffer.writerIndex());
        return tmp;
    }

    /**
     * Returns underlying buffer.
     *
     * @return
     */
    public ByteBuf getBuffer() {
        return _buffer;
    }


    public byte[] getSHA1() {
        try {
            MessageDigest md = MessageDigest.getInstance("SHA-1");
            md.update(_buffer.nioBuffer());
            return md.digest();
        } catch (Exception e) {
            return null;
        }
        //return Utilities.computeSHA1(getBytes());
    }


    public int resetReaderIndex() {
        _buffer.resetReaderIndex();
        return _buffer.readerIndex();
    }


    /**
     * Gets the length of the buffer
     * @return
     */
    public int length() {
        if (_limit == 0) {
            return _buffer.writerIndex();
        }
        return _limit;
    }

    public void advance(int position) {
        if (_buffer.readerIndex() < position) {
            _buffer.readerIndex(position);
        }
    }

    public void skipBytes(int count) {
        _buffer.readerIndex(_buffer.readerIndex() + count);
    }

    public boolean release() {
        return _buffer.release();
    }
}
