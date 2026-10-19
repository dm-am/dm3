// The readers of a game post, for the specs of the visual mode.
import type { Run } from "./markupReading";

/**
 * The words each reader reads, sorted: a stranger, the leads, the owners of
 * "Вася" and "Петя"; a hidden block with no names is the leads' alone.
 *
 * @param held What a reader takes away from a run: its words by default.
 */
export function readers(
  runs: Run[],
  held: (run: Run) => string[] = (run) => run.text.match(/Q\d{4}Q/g) ?? [],
): Record<string, string> {
  const read: Record<string, string[]> = {
    stranger: [],
    leads: [],
    Вася: [],
    Петя: [],
  };
  for (const run of runs) {
    const blocks = run.marks
      .split("|")
      .filter((mark) => mark.startsWith("private:"))
      .map((mark) =>
        mark
          .slice("private:".length)
          .split(",")
          .map((name) => name.toLowerCase()),
      );
    const words = held(run);
    read.leads.push(...words);
    if (blocks.length === 0) read.stranger.push(...words);
    for (const name of ["Вася", "Петя"]) {
      if (blocks.every((names) => names.includes(name.toLowerCase()))) {
        read[name].push(...words);
      }
    }
  }
  return Object.fromEntries(
    Object.entries(read).map(([reader, words]) => [
      reader,
      words.sort().join(" "),
    ]),
  );
}
