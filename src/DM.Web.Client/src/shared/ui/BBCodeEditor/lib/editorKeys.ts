/**
 * The keys of the editor. A chord is read off the physical key, so it works on
 * any layout, and the chords of the BBCode mode wrap the selection in a tag.
 * The visual mode binds its own two in editorExtensions.ts (visualEditorProps).
 */

/**
 * The letter of a key as the Latin layout has it. event.key is the letter the
 * active layout types, so on a Russian one Ctrl+B arrived as "и" and matched
 * nothing; event.code names the physical key.
 */
function letterOf(event: KeyboardEvent): string {
  const code = /^Key([A-Z])$/.exec(event.code);
  return code ? code[1].toLowerCase() : event.key.toLowerCase();
}

/** Ctrl (Cmd on a Mac) and Shift with a letter, and nothing else. */
export function isChord(
  event: KeyboardEvent,
  letter: string,
  shift: boolean,
): boolean {
  return (
    (event.ctrlKey || event.metaKey) &&
    !event.altKey &&
    event.shiftKey === shift &&
    letterOf(event) === letter
  );
}

/**
 * The chords of the BBCode mode: the letter, whether Shift is held, and the
 * tag the selection is wrapped in.
 */
export const SOURCE_CHORDS: ReadonlyArray<
  readonly [letter: string, shift: boolean, before: string, after: string]
> = [
  ["b", false, "[b]", "[/b]"],
  ["i", false, "[i]", "[/i]"],
  ["u", false, "[u]", "[/u]"],
  ["s", true, "[spoiler]", "[/spoiler]"],
];
