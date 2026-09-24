ephemeralMessage#d9c6dc1a flags:# out:flags.0?true id:int from_id:Peer peer_id:Peer receiver_id:long top_msg_id:flags.1?int date:int message:string entities:flags.2?Vector<MessageEntity> media:flags.3?MessageMedia reply_markup:flags.4?ReplyMarkup reply_to:flags.6?MessageReplyHeader = EphemeralMessage;
inputInvoiceStarGiftResale#c39f5324 flags:# ton:flags.0?true slug:string to_id:InputPeer = InputInvoice;
inputKeyboardButtonRequestPeer#02b78156 flags:# name_requested:flags.0?true username_requested:flags.1?true photo_requested:flags.2?true style:flags.10?KeyboardButtonStyle text:string button_id:int peer_type:RequestPeerType max_quantity:int = KeyboardButton;
inputKeyboardButtonUrlAuth#68013e72 flags:# request_write_access:flags.0?true style:flags.10?KeyboardButtonStyle text:string fwd_text:flags.1?string url:string bot:InputUser = KeyboardButton;
inputKeyboardButtonUserProfile#7d5e07c7 flags:# style:flags.10?KeyboardButtonStyle text:string user_id:InputUser = KeyboardButton;
inputSendMessageRichMessageDraftAction#e2b23b51 random_id:long rich_message:InputRichMessage = SendMessageAction;
keyboardButton#7d170cff flags:# style:flags.10?KeyboardButtonStyle text:string = KeyboardButton;
keyboardButtonBuy#3fa53905 flags:# style:flags.10?KeyboardButtonStyle text:string = KeyboardButton;
keyboardButtonCallback#e62bc960 flags:# requires_password:flags.0?true style:flags.10?KeyboardButtonStyle text:string data:bytes = KeyboardButton;
keyboardButtonCopy#bcc4af10 flags:# style:flags.10?KeyboardButtonStyle text:string copy_text:string = KeyboardButton;
keyboardButtonGame#89c590f9 flags:# style:flags.10?KeyboardButtonStyle text:string = KeyboardButton;
keyboardButtonRequestGeoLocation#aa40f94d flags:# style:flags.10?KeyboardButtonStyle text:string = KeyboardButton;
keyboardButtonRequestPeer#5b0f15f5 flags:# style:flags.10?KeyboardButtonStyle text:string button_id:int peer_type:RequestPeerType max_quantity:int = KeyboardButton;
keyboardButtonRequestPhone#417efd8f flags:# style:flags.10?KeyboardButtonStyle text:string = KeyboardButton;
keyboardButtonRequestPoll#7a11d782 flags:# style:flags.10?KeyboardButtonStyle quiz:flags.0?Bool text:string = KeyboardButton;
keyboardButtonSimpleWebView#e15c4370 flags:# style:flags.10?KeyboardButtonStyle text:string url:string = KeyboardButton;
keyboardButtonSwitchInline#991399fc flags:# same_peer:flags.0?true style:flags.10?KeyboardButtonStyle text:string query:string peer_types:flags.1?Vector<InlineQueryPeerType> = KeyboardButton;
keyboardButtonUrl#d80c25ec flags:# style:flags.10?KeyboardButtonStyle text:string url:string = KeyboardButton;
keyboardButtonUrlAuth#f51006f9 flags:# style:flags.10?KeyboardButtonStyle text:string fwd_text:flags.0?string url:string button_id:int = KeyboardButton;
keyboardButtonUserProfile#c0fd5d09 flags:# style:flags.10?KeyboardButtonStyle text:string user_id:long = KeyboardButton;
keyboardButtonWebView#e846b1a0 flags:# style:flags.10?KeyboardButtonStyle text:string url:string = KeyboardButton;
messageActionStarGiftUnique#e6c31522 flags:# upgrade:flags.0?true transferred:flags.1?true saved:flags.2?true refunded:flags.5?true prepaid_upgrade:flags.11?true assigned:flags.13?true from_offer:flags.14?true craft:flags.16?true gift:StarGift can_export_at:flags.3?int transfer_stars:flags.4?long from_id:flags.6?Peer peer:flags.7?Peer saved_id:flags.7?long resale_amount:flags.8?StarsAmount can_transfer_at:flags.9?int can_resell_at:flags.10?int drop_original_details_stars:flags.12?long can_craft_at:flags.15?int = MessageAction;
pageBlockBlockquote#263d7c26 text:RichText caption:RichText = PageBlock;
replyInlineMarkup#48a30254 rows:Vector<KeyboardButtonRow> = ReplyMarkup;
sendMessageRichMessageDraftAction#a2cb24f9 random_id:long rich_message:RichMessage = SendMessageAction;
sendMessageTextDraftAction#376d975c random_id:long text:TextWithEntities = SendMessageAction;
updateEphemeralBotCallbackQuery#9b380762 query_id:long user_id:long peer:Peer msg_id:int data:bytes message:EphemeralMessage = Update;

---functions---

ephemeral.deleteMessage#a3c0d511 peer:InputPeer receiver_id:InputUser id:int = Bool;
ephemeral.editMessage#13f250ee flags:# peer:InputPeer receiver_id:InputUser id:int message:flags.0?string media:flags.3?InputMedia entities:flags.1?Vector<MessageEntity> reply_markup:flags.2?ReplyMarkup = Updates;
ephemeral.sendMessage#68cbd09f flags:# peer:InputPeer receiver_id:InputUser query_id:flags.0?long message:string entities:flags.1?Vector<MessageEntity> media:flags.2?InputMedia reply_markup:flags.3?ReplyMarkup rich_message:flags.4?InputRichMessage random_id:long reply_to:flags.5?InputReplyTo = Updates;
