// Constructors published at layer 133 that the official Android client still
// sends while announcing LAYER 214. Each line reproduces its own constructor
// id from the CRC32 of its text (tools/tl-crc32.py).

---functions---

// Telegram-Android 12.0.1 (TLRPC.TL_messages_addChatUser) still sends this id.
// Current: messages.addChatUser#cbc6d107, whose fields are identical and whose
// result became messages.InvitedUsers. Android already deserializes that current
// result. Published through layer 176.
messages.addChatUser#f24753e3 chat_id:long user_id:InputUser fwd_limit:int = Updates;
