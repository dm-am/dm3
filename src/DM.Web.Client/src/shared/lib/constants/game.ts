/**
 * The longest name of a character: a copy of CharacterPolicy.cs and the width
 * of its column, so the form stops the typing where the save refuses. A
 * server-side test compares the three numbers.
 */
export const CHARACTER_NAME_MAX_LENGTH = 40;

/**
 * The longest title and the shortest description of a game
 * (GameFieldLimits.cs), title of an attribute schema or attribute
 * (AttributeSchemaFieldLimits.cs) and title of a room (RoomFieldLimits.cs).
 * ClientFieldLimitsShould compares the numbers. The system and the setting
 * have no bound in the form, as on DM2: only the server bounds them.
 */
// As DM2's form: a title is the name a game goes by in the lists, not a sentence.
export const GAME_TITLE_MAX_LENGTH = 50;
// A game is chosen by strangers from this text alone; DM2 asked as much of it.
export const GAME_INFO_MIN_LENGTH = 100;
export const ATTRIBUTE_SCHEMA_TITLE_MAX_LENGTH = 100;
export const ROOM_TITLE_MAX_LENGTH = 100;
