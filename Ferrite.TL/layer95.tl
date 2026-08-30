// Constructors published at layer 95 that official clients still send at
// LAYER 214. Handlers read these and delegate to the current implementation;
// nothing here forks server behavior. Each line reproduces its own constructor
// id from the CRC32 of its text (tools/tl-crc32.py). Layer 94 is not published
// anywhere public, so this shape may be one layer older than the file's name.

---functions---

// Telegram-Android 12.0.1 (TMessagesProj TL_account.uploadWallPaper) still
// sends this shape, which has no flags word. Current:
// account.uploadWallPaper#e39a8f03 adds flags and an optional for_chat, which
// this shape cannot set. Published through layer 155.
account.uploadWallPaper#dd853661 file:InputFile mime_type:string settings:WallPaperSettings = WallPaper;
