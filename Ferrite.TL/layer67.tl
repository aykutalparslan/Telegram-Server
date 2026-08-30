// Constructors published at layer 67 that official clients still send at
// LAYER 214. Handlers read these and delegate to the current implementation;
// nothing here forks server behavior. Each line reproduces its own constructor
// id from the CRC32 of its text (tools/tl-crc32.py).

---types---

// Element of the vector langpack.getLanguages answers with, in its current
// shape. The generator keys combinators by constructor id and keeps the first
// declaration, so this resolves to the baseLayer type already generated.
langPackLanguage#eeca5ce3 flags:# official:flags.0?true rtl:flags.2?true beta:flags.3?true name:string native_name:string lang_code:string base_lang_code:flags.1?string plural_code:string strings_count:int translated_count:int translations_url:string = LangPackLanguage;

---functions---

// Telegram-Android 12.0.1 (TLRPC.TL_langpack_getLangPack) omits lang_pack.
// Current: langpack.getLangPack#f2f2330a. Published through layer 84.
langpack.getLangPack#9ab5c58e lang_code:string = LangPackDifference;

// Telegram-Android 12.0.1 (TLRPC.TL_langpack_getStrings) omits lang_pack.
// Current: langpack.getStrings#efea3803. Published through layer 84.
langpack.getStrings#2e1ee318 lang_code:string keys:Vector<string> = Vector<LangPackString>;

// Telegram-Android 12.0.1 (TMessagesProj TLRPC.TL_langpack_getLanguages) sends
// the parameterless shape. Current: langpack.getLanguages#42c6978f
// lang_pack:string. Published through layer 84.
langpack.getLanguages#800fd57d = Vector<LangPackLanguage>;

// Telegram-Android 12.0.1 (TLRPC.TL_messages_uploadMedia) omits the current
// flags word and optional business connection. Current:
// messages.uploadMedia#14967978. Published through layer 176.
messages.uploadMedia#519bc2b1 peer:InputPeer media:InputMedia = MessageMedia;
