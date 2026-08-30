// Constructors published at layer 104 that official clients still send at
// LAYER 214. Handlers read these and delegate to the current implementation;
// nothing here forks server behavior. Each line reproduces its own constructor
// id from the CRC32 of its text (tools/tl-crc32.py); auth.authorization is
// carried verbatim from TDLib's telegram_api.tl and does not reproduce under
// that tool's normalization, and neither does TDLib's own copy of it.

---types---

// Results of auth.signUp, in their current shape. The generator keys
// combinators by constructor id and keeps the first declaration, so these
// resolve to the baseLayer types already generated for the same ids.
auth.authorization#2ea2c0d4 flags:# setup_password_required:flags.1?true otherwise_relogin_days:flags.1?int tmp_sessions:flags.0?int future_auth_token:flags.2?bytes user:User = auth.Authorization;
auth.authorizationSignUpRequired#44747e9a flags:# terms_of_service:flags.0?help.TermsOfService = auth.Authorization;

---functions---

// Telegram-Android 12.0.1 (TMessagesProj TLRPC.TL_auth_signUp) and every build
// since still serializes this pre-flags shape. Current: auth.signUp#aac7b717.
// Answers auth.authorization#2ea2c0d4 or auth.authorizationSignUpRequired#44747e9a.
// Published through layer 171.
auth.signUp#80eee427 phone_number:string phone_code_hash:string first_name:string last_name:string = auth.Authorization;
