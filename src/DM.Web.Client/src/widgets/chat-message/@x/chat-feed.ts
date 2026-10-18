/**
 * Cross-import surface for widgets/chat-feed (FSD @x).
 *
 * The feed of the global chat and of a correspondence draws its messages with
 * this widget, the one the chat room of a game draws them with: a second
 * renderer would part from it at the first edit.
 */
export { ChatMessage } from "..";
