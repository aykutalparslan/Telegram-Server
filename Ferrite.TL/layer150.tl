// Constructors published at layer 150 that official clients still send at
// LAYER 214. Handlers read these and delegate to the current implementation;
// nothing here forks server behavior. Each line reproduces its own constructor
// id from the CRC32 of its text (tools/tl-crc32.py).

---functions---

// Telegram-Android 12.0.1 (TMessagesProj TLRPC.TL_messages_createChat) still
// sends this id. Current: messages.createChat#92ceddd4, whose field list is
// identical -- the id moved when the result type became messages.InvitedUsers,
// and the client did not follow. The same client deserializes the reply as
// TL_messages_invitedUsers (TLRPC.java), so the declared Updates result is only
// what the published line said; Ferrite answers the current
// messages.invitedUsers and the client reads it. Published through layer 176.
messages.createChat#34a818 flags:# users:Vector<InputUser> title:string ttl_period:flags.0?int = Updates;
