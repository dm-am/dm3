/**
 * @vitest-environment node
 */

/**
 * Four helpers that exist, and the four shapes of writing them again.
 *
 * None of these is hypothetical, and in three of the four the SSOT was imported
 * two lines above its own copy:
 *
 * 1. `describeFailure` reads the per-field validation codes first and falls
 *    back to the problem document's title. Nine call sites read `.title`
 *    directly, so a rejected character sheet printed the API's English
 *    "Validation failed" under a Russian form while the codes that said what to
 *    correct were in the same response.
 * 2. `unwrapResource` reads `{ resource }` or a bare payload, with a typeof
 *    check. Three stores wrote it by hand with a cast through `unknown`, which
 *    silences the compiler in the one place the shape is genuinely unknown and
 *    turns an empty body into a Game.
 * 3. `errorCodeForStatus` maps an HTTP status to an error page. Three pages had
 *    their own copy and they had diverged: a board refused with 410 drew
 *    "ошибка сервера" while a topic in that board drew the right page.
 * 4. `useViewerChange` answers "the viewer changed". Six sidebar blocks asked
 *    "appeared or disappeared" instead, and a second tab signing another
 *    account in is neither, so the previous viewer's private games stayed on
 *    screen under the new name.
 *
 * Read through the AST, so a copy quoted in a comment is not a violation, and
 * `console` arguments are exempt: a developer log is not interface copy. Spec
 * files are skipped, because a test names the wrong shape in order to assert on
 * it.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";
import ts from "typescript";
import { parse as parseSfc } from "vue/compiler-sfc";

/** This spec sits at the root of the client sources. */
const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

/** The names a problem document travels under in this codebase. */
const ERROR_NAMES = new Set(["error", "apiError", "fetchError", "err", "e"]);

function sources(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) sources(full, out);
    } else if (
      (full.endsWith(".ts") || full.endsWith(".vue")) &&
      !full.endsWith(".spec.ts") &&
      !full.endsWith(".d.ts")
    ) {
      out.push(full);
    }
  }
  return out;
}

const FILES = sources(CLIENT_SRC);

const where = (file: string) => relative(CLIENT_SRC, file).replace(/\\/g, "/");

/** The script of a single-file component, or the whole file for a module. */
function scriptOf(file: string): string {
  const raw = readFileSync(file, "utf8");
  if (!file.endsWith(".vue")) return raw;
  const { descriptor } = parseSfc(raw);
  return descriptor.scriptSetup?.content ?? descriptor.script?.content ?? "";
}

/** Every node of every source, with the file it came from. */
function findAll(
  match: (node: ts.Node) => boolean,
  skip: (path: string) => boolean = () => false,
): string[] {
  const found: string[] = [];
  for (const file of FILES) {
    const path = where(file);
    if (skip(path)) continue;
    const code = scriptOf(file);
    if (!code) continue;
    const source = ts.createSourceFile(
      file,
      code,
      ts.ScriptTarget.Latest,
      true,
    );
    const visit = (node: ts.Node): void => {
      if (match(node)) {
        const line =
          source.getLineAndCharacterOfPosition(node.getStart()).line + 1;
        found.push(`${path}: ${node.getText().split("\n")[0]} (line ${line})`);
      }
      ts.forEachChild(node, visit);
    };
    visit(source);
  }
  return found;
}

/** True when the node sits inside a console.* call. */
function insideConsoleCall(node: ts.Node): boolean {
  for (let current = node.parent; current; current = current.parent) {
    if (
      ts.isCallExpression(current) &&
      ts.isPropertyAccessExpression(current.expression) &&
      ts.isIdentifier(current.expression.expression) &&
      current.expression.expression.text === "console"
    ) {
      return true;
    }
  }
  return false;
}

describe("shared helpers are not written a second time", () => {
  it("reads a failed request's sentence through describeFailure", () => {
    const offenders = findAll(
      (node) =>
        ts.isPropertyAccessExpression(node) &&
        node.name.text === "title" &&
        ts.isIdentifier(node.expression) &&
        ERROR_NAMES.has(node.expression.text) &&
        !insideConsoleCall(node),
      (path) => path.startsWith("shared/lib/errors/"),
    );

    expect(
      offenders,
      "read the problem document through describeFailure(error, fallback): it prefers the per-field codes and the API fills title in English",
    ).toEqual([]);
  });

  it("unwraps the single-resource envelope through unwrapResource", () => {
    const offenders = findAll(
      (node) => {
        if (!ts.isBinaryExpression(node)) return false;
        // `x.resource ?? (x as unknown as T)`: a nullish default of null or of
        // some other value is a different thing and stays allowed.
        if (
          node.operatorToken.kind === ts.SyntaxKind.QuestionQuestionToken &&
          ts.isPropertyAccessExpression(node.left) &&
          node.left.name.text === "resource" &&
          ts.isAsExpression(node.right)
        ) {
          return true;
        }
        return (
          node.operatorToken.kind === ts.SyntaxKind.InKeyword &&
          ts.isStringLiteral(node.left) &&
          node.left.text === "resource"
        );
      },
      (path) => path === "shared/api/envelope.ts",
    );

    expect(
      offenders,
      "use unwrapResource<T>(payload): it checks the payload is an object, which the hand-written form does not",
    ).toEqual([]);
  });

  it("maps an HTTP status to an error page through errorCodeForStatus", () => {
    const returnsPageCode = (node: ts.Node): boolean => {
      let found = false;
      const visit = (child: ts.Node): void => {
        if (
          ts.isReturnStatement(child) &&
          child.expression &&
          ts.isNumericLiteral(child.expression) &&
          ["400", "401", "403", "404", "409", "410", "500"].includes(
            child.expression.text,
          )
        ) {
          found = true;
        }
        ts.forEachChild(child, visit);
      };
      ts.forEachChild(node, visit);
      return found;
    };

    const offenders = findAll(
      (node) =>
        (ts.isFunctionDeclaration(node) || ts.isArrowFunction(node)) &&
        node.parameters.some((p) => p.name.getText() === "status") &&
        returnsPageCode(node),
      (path) => path.startsWith("shared/ui/ErrorPage/"),
    );

    expect(
      offenders,
      "use errorCodeForStatus from shared/ui/ErrorPage: three copies of this map had already diverged",
    ).toEqual([]);
  });

  it("answers a change of viewer through useViewerChange", () => {
    const offenders = findAll(
      (node) =>
        ts.isCallExpression(node) &&
        ts.isIdentifier(node.expression) &&
        node.expression.text === "watch" &&
        node.arguments.length > 0 &&
        node.arguments[0].getText().includes("user?.username"),
      (path) => path === "shared/lib/composables/useViewerChange.ts",
    );

    expect(
      offenders,
      "use useViewerChange(handler): a hand-written watch tests for a name appearing or disappearing, and a second tab swapping accounts is neither",
    ).toEqual([]);
  });
});
