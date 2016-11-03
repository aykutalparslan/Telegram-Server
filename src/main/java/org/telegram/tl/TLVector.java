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
import org.telegram.tl.service.message;

import java.util.*;

/**
 * Created by aykut on 21/10/15.
 */
public class TLVector<T> extends TLObject implements List<T> {

    public static final int ID = 0x1cb5c415;

    private ArrayList<T> items = new ArrayList<T>();
    private Class destClass = TLObject.class;

    public void setDestClass(Class c) {
        destClass = c;
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(64);
        serializeTo(buffer);
        return buffer;
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(items.size());

        if(items.size() > 0){
            destClass = items.get(0).getClass();

            if (destClass == Integer.class) {
                for (T i : items) {
                    buff.writeInt((Integer) i);
                }
            } else if (destClass == Long.class) {
                for (T i : items) {
                    buff.writeLong((Long) i);
                }
            } else if (destClass == String.class) {
                for (T i : items) {
                    buff.writeString((String) i);
                }
            } else {
                for (T i : items) {
                    buff.writeTLObject((TLObject) i);
                }
            }
        }
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {

        int count = buffer.readInt();
        for (int i = 0; i < count; i++) {
            if (destClass == Integer.class) {
                items.add((T) (Integer) buffer.readInt());
            } else if (destClass == Long.class) {
                items.add((T) (Long) buffer.readLong());
            } else if (destClass == String.class) {
                items.add((T) buffer.readString());
            } else if (destClass == message.class) {
                items.add((T) buffer.readBareTLType(new message()));
            } else {
                items.add((T) APIContext.getInstance().deserialize(buffer));
            }
        }
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public int size() {
        return items.size();
    }

    @Override
    public boolean isEmpty() {
        return items.isEmpty();
    }

    @Override
    public boolean contains(Object o) {
        return items.contains(o);
    }

    @Override
    public Iterator<T> iterator() {
        return items.iterator();
    }

    @Override
    public Object[] toArray() {
        return items.toArray();
    }

    @Override
    public <T1> T1[] toArray(T1[] t1s) {
        return items.toArray(t1s);
    }

    @Override
    public boolean add(T t) {
        return items.add(t);
    }

    @Override
    public boolean remove(Object o) {
        return items.remove(o);
    }

    @Override
    public boolean containsAll(Collection<?> objects) {
        return items.containsAll(objects);
    }

    @Override
    public boolean addAll(Collection<? extends T> ts) {
        return items.addAll(ts);
    }

    @Override
    public boolean addAll(int i, Collection<? extends T> ts) {
        return items.addAll(i, ts);
    }

    @Override
    public boolean removeAll(Collection<?> objects) {
        return items.removeAll(objects);
    }

    @Override
    public boolean retainAll(Collection<?> objects) {
        return items.retainAll(objects);
    }

    @Override
    public void clear() {
        items.clear();
    }

    @Override
    public T get(int i) {
        return items.get(i);
    }

    @Override
    public T set(int i, T t) {
        return items.set(i, t);
    }

    @Override
    public void add(int i, T t) {
        items.add(i, t);
    }

    @Override
    public T remove(int i) {
        return items.remove(i);
    }

    @Override
    public int indexOf(Object o) {
        return items.indexOf(o);
    }

    @Override
    public int lastIndexOf(Object o) {
        return items.lastIndexOf(o);
    }

    @Override
    public ListIterator<T> listIterator() {
        return items.listIterator();
    }

    @Override
    public ListIterator<T> listIterator(int i) {
        return items.listIterator(i);
    }

    @Override
    public List<T> subList(int i, int i2) {
        return items.subList(i, i2);
    }

    @Override
    public String toString() {
        return "vector#1cb5c415";
    }
}
