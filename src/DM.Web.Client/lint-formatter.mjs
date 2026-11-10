/**
 * ESLint's default output with a hint under each size finding: the default
 * formatter's text as it is, and under every row of a rule that measures size,
 * one line on how to cut. The rule's own message says only that a limit was
 * passed, and the quick way under a limit, a split by line count, leaves two
 * halves nobody can name.
 *
 * Used by `npm run lint:ci` and by the agent hooks in .claude/hooks, as
 * `--format ./lint-formatter.mjs`.
 */
import { createRequire } from "node:module";
import { dirname, join } from "node:path";
import { stripVTControlCharacters } from "node:util";

const require = createRequire(import.meta.url);

// The package exports no formatter, so the default one is loaded by its path.
const stylish = require(
  join(
    dirname(require.resolve("eslint/package.json")),
    "lib/cli-engine/formatters/stylish.js",
  ),
);

const SIZE_RULES = [
  "complexity",
  "max-lines",
  "max-lines-per-function",
  "max-depth",
  "vue/max-lines-per-block",
];

const HINT =
  "hint: cut by meaning, each part named after what it does, never by size alone";

/**
 * A row of the default output ends with the rule id, after the message; a
 * plugin's id, prefix and slash included, is matched whole.
 */
const SIZE_ROW = new RegExp(`\\s(${SIZE_RULES.join("|")})$`);

export default function format(results, context) {
  return stylish(results, context)
    .split("\n")
    .flatMap((line) =>
      SIZE_ROW.test(stripVTControlCharacters(line).trimEnd())
        ? [line, `  ${HINT}`]
        : [line],
    )
    .join("\n");
}
