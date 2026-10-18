import type { ChatActionResult } from "@/shared/lib/composables/useChatComposer";

/**
 * The height a long message folds at, in either feed, as a picture in a post
 * is held to.
 */
export const MESSAGE_FOLD_HEIGHT = 500;

/** What the store of a feed answers to what the reader does in it. */
export interface ChatFeedActions {
  send: (text: string) => Promise<ChatActionResult>;
  remove: (id: string) => Promise<ChatActionResult>;
  like: (id: string) => Promise<ChatActionResult>;
  unlike: (id: string) => Promise<ChatActionResult>;
  /**
   * The BBCode of a message, to seed its edit box: the text as the author
   * wrote it, read back through htmlToBbcode (authorEditRoundTrip.spec.ts).
   */
  editText: (id: string) => Promise<string>;
  saveEdit: (id: string, text: string) => Promise<ChatActionResult>;
}
