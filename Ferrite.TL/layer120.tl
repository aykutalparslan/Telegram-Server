// Constructors published at layer 120 that official clients still send at
// LAYER 214. Handlers read these and delegate to the current implementation;
// nothing here forks server behavior. Each line reproduces its own constructor
// id from the CRC32 of its text (tools/tl-crc32.py).

---functions---

// Telegram-Android 12.0.1 (TMessagesProj TLRPC.TL_messages_unpinAllMessages)
// still sends this shape, which has only peer. Current:
// messages.unpinAllMessages#62dd747 adds a flags word and optional topic/saved
// peer fields. Published through layer 147.
messages.unpinAllMessages#f025bc8b peer:InputPeer = messages.AffectedHistory;
