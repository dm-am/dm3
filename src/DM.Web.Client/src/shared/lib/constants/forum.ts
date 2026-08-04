/**
 * The longest title a forum topic may be saved with.
 *
 * The server holds the same number in TopicPolicy.cs, and both validators
 * refuse a longer one. The browser can read neither, so the two forms that
 * write a title keep this copy and stop the typing at the same character. The
 * create form used to stop at two hundred, which is what the request contract
 * promised as well: a title in between was typed in full and then refused by
 * the save, with a message that named neither the field nor a number. A
 * server-side test compares the numbers.
 */
export const TOPIC_TITLE_MAX_LENGTH = 130;
