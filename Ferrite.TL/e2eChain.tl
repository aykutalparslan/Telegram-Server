// Ferrite-local mirror of the chain half of
// TDLib td/generate/scheme/e2e_api.tl.
//
// Constructor ids are PINNED to the reference's generated ids
// (TDLib td/generate/auto/td/telegram/e2e_api.h). Blocks are hashed
// and Ed25519-signed over these exact bytes, so an id or field-order change
// silently breaks every client's chain rather than failing loudly.
//
// Ferrite's lexer supports one namespace segment, so `e2e.chain.block` is
// written `e2e.chainBlock`. Only the C# name changes; the wire bytes do not.
//
// `vector<...>` here is a BARE vector: the reference serializes these with
// TlStoreVector, which writes only an int32 count and never the 1cb5c415
// constructor.

e2e.chainGroupParticipant#18f3971f user_id:long public_key:int256 flags:# add_users:flags.0?true remove_users:flags.1?true version:int = e2e.ChainGroupParticipant;
e2e.chainGroupState#1ddc7584 participants:vector<e2e.ChainGroupParticipant> external_permissions:int = e2e.ChainGroupState;
e2e.chainSharedKey#8a847e7f ek:int256 encrypted_shared_key:string dest_user_id:vector<long> dest_header:vector<bytes> = e2e.ChainSharedKey;

e2e.chainChangeNoop#deb4a41b nonce:int256 = e2e.ChainChange;
e2e.chainChangeSetValue#fe0139cc key:bytes value:bytes = e2e.ChainChange;
e2e.chainChangeSetGroupState#2cf17146 group_state:e2e.ChainGroupState = e2e.ChainChange;
e2e.chainChangeSetSharedKey#987a2158 shared_key:e2e.ChainSharedKey = e2e.ChainChange;

e2e.chainStateProof#d6b679e6 flags:# kv_hash:int256 group_state:flags.0?e2e.ChainGroupState shared_key:flags.1?e2e.ChainSharedKey = e2e.ChainStateProof;

e2e.chainBlock#639a3db6 signature:int512 flags:# prev_block_hash:int256 changes:vector<e2e.ChainChange> height:int state_proof:e2e.ChainStateProof signature_public_key:flags.0?int256 = e2e.ChainBlock;

e2e.chainGroupBroadcastNonceCommit#d1512ae7 signature:int512 user_id:long chain_height:int chain_hash:int256 nonce_hash:int256 = e2e.ChainGroupBroadcast;
e2e.chainGroupBroadcastNonceReveal#83f4f9d8 signature:int512 user_id:long chain_height:int chain_hash:int256 nonce:int256 = e2e.ChainGroupBroadcast;
