// Constructors present by Telegram layer 2 that the official Android client
// still sends while announcing LAYER 214. Layers before 2 are unavailable in
// the published schema archive, so this shape may originate in layer 1.
// Each line reproduces its own constructor id (tools/tl-crc32.py).

---functions---

// Telegram-Android 12.0.1 (TLRPC.TL_messages_getMessages) still sends bare
// message ids. Current: messages.getMessages#63c66506, whose ids are
// InputMessage values. Published through layer 75.
messages.getMessages#4222fa74 id:Vector<int> = messages.Messages;
