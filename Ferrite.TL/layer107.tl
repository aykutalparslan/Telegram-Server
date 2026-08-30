// Constructors published at layer 107 that the official Android client still
// sends while announcing LAYER 214. Each line reproduces its own constructor
// id from the CRC32 of its text (tools/tl-crc32.py).

---functions---

// Telegram-Android 12.0.1 (TL_account.createTheme) still sends one optional
// InputThemeSettings. Current account.createTheme#652e4400 sends a vector.
// Published through layer 133.
account.createTheme#8432c21f flags:# slug:string title:string document:flags.2?InputDocument settings:flags.3?InputThemeSettings = Theme;

// Telegram-Android 12.0.1 (TL_account.updateTheme) still sends one optional
// InputThemeSettings. Current account.updateTheme#2bf40ccc sends a vector.
// Published through layer 133.
account.updateTheme#5cb367d5 flags:# format:string theme:InputTheme slug:flags.0?string title:flags.1?string document:flags.2?InputDocument settings:flags.3?InputThemeSettings = Theme;
