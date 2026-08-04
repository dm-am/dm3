/**
 * The longest name a character may be saved with.
 *
 * The server holds the same number in CharacterPolicy.cs and stores the name in
 * a column of that width. The browser can read neither, so the form keeps this
 * copy and stops the typing at the same character. A server-side test compares
 * the three numbers: raising one of them alone is how a form comes to accept a
 * name the save then refuses, with a message that names neither the field nor a
 * number.
 */
export const CHARACTER_NAME_MAX_LENGTH = 40;
