// Android-only constructor carried by the official Telegram-Android 12.0.1
// generated schema while it announces LAYER 214. No published Telegram schema
// in the available archive contains this declaration, so its original layer is
// unknown. The declaration is byte-identical to TLRPC.TL_messages_reportReaction
// at ddc90f16be1 and reproduces #61422a48 from its CRC32. The published method
// first appears at layer 145 with InputPeer reaction_peer and #3f64c076.

---functions---

messages.reportReaction#61422a48 peer:InputPeer id:int user_id:InputUser = Bool;
