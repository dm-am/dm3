// Every document and the architecture tests that hold it.
//
// Two ways a test holds a document, both read from the lines of its code, not of
// its comments:
//
//   - it walks the documents: within a few lines it filters files by ".md" (a
//     "*.md" pattern or EndsWith(".md")) and takes them from a whole tree ("docs"
//     or RepositoryFiles.CommittableFiles()). Such a test holds every document
//     at once and is listed once, not against each of them;
//   - it opens one document: its code names the path of the document from the
//     root, either whole in one string literal ("docs/STACK.md") or as one
//     literal per segment within a few lines (Path.Combine(root, "docs",
//     "guides", "TESTING.md"), or a list of names combined with "docs",
//     "guides" right below it). A walker that also opens a document this way is
//     listed against it too. A name inside a longer string (a message, a
//     reason) is not a literal of it, a file name joined to some other
//     directory is not the document, and a line that writes a file builds a
//     fixture, not a read.
//
// A line that only records a document in a register is not a read of it: a row
// new("docs/X.md", ...) of a list of exceptions, a bare literal row of a
// list of names, or a case of [InlineData]. Such a test holds the document only
// to the count the register gives, and the cleanup that empties the register
// would otherwise read as coverage lost. Those are listed in a column of their
// own and stay out of the count of documents with a test.
//
// Only a class named ...Should is a test. A shared helper that the gates call is
// not one: the tests that call HandwrittenDocuments.All walk every document
// through it, and are listed as walkers themselves.
//
// The match is by text, so a test that names a file of the same name elsewhere
// in the tree is counted too.
import { basename, join } from "node:path";
import { ROOT, compare, read, rel, walk } from "./repo.mjs";

const TESTS = join(ROOT, "test", "DM.Architecture.Tests");

/** A line that is only comment: `//`, `///`, or a line of a block comment. */
const COMMENT = /^\s*(?:\/\/|\/\*|\*)/;

/** How many lines apart the filter and the tree of one walk may stand. */
const WALK_WINDOW = 10;

/** How many lines apart the segments of one path may stand. */
const PATH_WINDOW = 3;

const MARKDOWN_FILTER = /"\*\.md"|EndsWith\("\.md"/;
const HELPER_WALK = /\bHandwrittenDocuments\.All\b/;
const REGISTER_ROW = /^\s*(?:new\s*\(\s*"[^"]*"\s*,|"[^"]*",?\s*$|\[InlineData\()/;
const TEST_CLASS = /Should$/;
const WHOLE_TREE = /"docs"|CommittableFiles\(\)/;
const WRITES_FILE = /\bFile\.Write/;
const LITERAL = /"((?:[^"\\]|\\.)*)"/g;

function codeLines(path) {
  return read(path)
    .split("\n")
    .filter((line) => !COMMENT.test(line));
}

function walksDocuments(lines) {
  if (lines.some((line) => HELPER_WALK.test(line))) return true;
  return lines.some((line, i) => {
    if (!MARKDOWN_FILTER.test(line)) return false;
    const around = lines.slice(Math.max(0, i - WALK_WINDOW), i + WALK_WINDOW + 1);
    return around.some((near) => WHOLE_TREE.test(near));
  });
}

/** The string literals of every line, the lines that write a file left empty. */
function literalLines(lines) {
  return lines.map((line) =>
    WRITES_FILE.test(line) ? new Set() : new Set([...line.matchAll(LITERAL)].map((match) => match[1])),
  );
}

/** Whether the lines name the path, whole or segment by segment close together. */
function opens(literals, path) {
  const segments = path.split("/");
  return literals.some((line, i) => {
    if (line.has(path)) return true;
    if (!line.has(segments.at(-1))) return false;
    const around = literals.slice(Math.max(0, i - PATH_WINDOW), i + PATH_WINDOW + 1);
    return segments.every((segment) => around.some((near) => near.has(segment)));
  });
}

/** Every test class, a partial class once per file, with what its lines read and record. */
function readTests() {
  return walk(TESTS, (p) => p.endsWith(".cs"))
    .map((path) => ({ path, name: basename(path, ".cs").split(".")[0] }))
    .filter((file) => TEST_CLASS.test(file.name))
    .map(({ path, name }) => {
      const lines = codeLines(path);
      const rows = lines.map((line) => REGISTER_ROW.test(line));
      const literals = literalLines(lines);
      return {
        name,
        walks: walksDocuments(lines),
        reads: literals.map((set, i) => (rows[i] ? new Set() : set)),
        records: literals.map((set, i) => (rows[i] ? set : new Set())),
      };
    });
}

/** The names of the tests whose lines of the given kind name the path, once each. */
function naming(tests, kind, path) {
  return [...new Set(tests.filter((test) => opens(test[kind], path)).map((test) => test.name))].sort(compare);
}

/**
 * The tests that walk every document, and for each document the tests that open
 * it and the tests that only record it in a register.
 */
export function readDocuments() {
  const documents = [join(ROOT, "README.md"), ...walk(join(ROOT, "docs"), (p) => p.endsWith(".md"))];
  const tests = readTests();
  const walkers = [...new Set(tests.filter((test) => test.walks).map((test) => test.name))].sort(compare);
  const list = documents
    .map((path) => {
      const repoPath = rel(path);
      const reads = naming(tests, "reads", repoPath);
      return {
        path: repoPath,
        tests: reads,
        registers: naming(tests, "records", repoPath).filter((name) => !reads.includes(name)),
      };
    })
    .sort((a, b) => compare(a.path, b.path));
  return { walkers, list };
}
