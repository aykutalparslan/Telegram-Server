// Constructors present by Telegram layer 23 that the official Android client
// still sends while announcing LAYER 214. Layers 19..22 are unavailable in
// the published schema archive, so this shape may originate earlier.
// Each line reproduces its own constructor id (tools/tl-crc32.py).

---functions---

// Telegram-Android 12.0.1 (TL_account.deleteAccount) still sends the pre-SRP
// shape. Current: account.deleteAccount#a2c0cf74 adds a flags word and optional
// password proof. Published through layer 142.
account.deleteAccount#418d4e0b reason:string = Bool;
