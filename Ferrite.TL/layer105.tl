// Constructors published at layer 105 that the official Android client still
// sends while announcing LAYER 214. Each line reproduces its own constructor
// id from the CRC32 of its text (tools/tl-crc32.py).

---functions---

// Telegram-Android 12.0.1 (TL_account.getTheme) still sends document_id.
// Current: account.getTheme#3a5869ec drops that field. Published through 147.
account.getTheme#8d9d742b format:string theme:InputTheme document_id:long = Theme;

// Telegram-Android 12.0.1 (TL_account.installTheme) couples format and theme
// under bit 1 and has no base_theme. Current: account.installTheme#c727bb3b.
// Published through layer 133.
account.installTheme#7ae43737 flags:# dark:flags.0?true format:flags.1?string theme:flags.1?InputTheme = Bool;
