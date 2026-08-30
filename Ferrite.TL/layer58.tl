// Constructors published at layer 58 that official clients still send at
// LAYER 214. Handlers read these and delegate to the current implementation;
// nothing here forks server behavior. Each line reproduces its own constructor
// id from the CRC32 of its text (tools/tl-crc32.py).

---types---

// Results of updates.getDifference, in their current shape. The generator keys
// combinators by constructor id and keeps the first declaration, so these
// resolve to the baseLayer types already generated for the same ids.
updates.differenceEmpty#5d75a138 date:int seq:int = updates.Difference;
updates.difference#f49ca0 new_messages:Vector<Message> new_encrypted_messages:Vector<EncryptedMessage> other_updates:Vector<Update> chats:Vector<Chat> users:Vector<User> state:updates.State = updates.Difference;
updates.differenceSlice#a8fb1981 new_messages:Vector<Message> new_encrypted_messages:Vector<EncryptedMessage> other_updates:Vector<Update> chats:Vector<Chat> users:Vector<User> intermediate_state:updates.State = updates.Difference;
updates.differenceTooLong#4afe8f6d pts:int = updates.Difference;

---functions---

// Telegram-Android 12.0.1 (TMessagesProj TLRPC.TL_updates_getDifference) sends
// the shape without pts_limit and qts_limit. Current:
// updates.getDifference#19c2f763. Flag bit 0 means pts_total_limit in both, so
// the request widens without moving a field. Published through layer 158.
updates.getDifference#25939651 flags:# pts:int pts_total_limit:flags.0?int date:int qts:int = updates.Difference;
