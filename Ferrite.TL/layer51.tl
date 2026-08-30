// Constructors published at layer 51 that official clients still send at
// LAYER 214. Handlers read these and delegate to the current implementation;
// nothing here forks server behavior. Each line reproduces its own constructor
// id from the CRC32 of its text (tools/tl-crc32.py). Layers 26..50 are not
// published anywhere public, so both shapes may be older than this file's name.

---functions---

// Telegram-Android 12.0.1 (TMessagesProj TLRPC.TL_channels_getMessages)
// still sends message ids as bare ints. Current: channels.getMessages#ad8c9a23,
// whose ids are InputMessage values. Published through layer 75.
channels.getMessages#93d7b347 channel:InputChannel id:Vector<int> = messages.Messages;

// Telegram-Android 12.0.1 (TMessagesProj TLRPC.TL_channels_inviteToChannel)
// still sends this id. Current: channels.inviteToChannel#c9e33d54, whose field
// list is identical -- the id moved when the result type became
// messages.InvitedUsers, and the client did not follow. That client
// deserializes the reply as TL_messages_invitedUsers (TLRPC.java), so the
// declared Updates result is only what the published line said; Ferrite answers
// the current messages.invitedUsers and the client reads it. Published through
// layer 176.
channels.inviteToChannel#199f3a6c channel:InputChannel users:Vector<InputUser> = Updates;
