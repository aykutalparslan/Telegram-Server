// Constructors published at layer 135 that the official Android client still
// sends while announcing LAYER 214. Each line reproduces its own constructor
// id from the CRC32 of its text (tools/tl-crc32.py).

---functions---

// Telegram-Android 12.0.1 (TLRPC.TL_channels_getSendAs) still sends the
// peer-only shape. Current: channels.getSendAs#e785a43f adds a flags word and
// for_paid_reactions. Published through layer 198.
channels.getSendAs#0dc770ee peer:InputPeer = channels.SendAsPeers;
