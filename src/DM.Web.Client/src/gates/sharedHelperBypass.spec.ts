/**
 * @vitest-environment node
 */

/**
 * Four helpers that exist, and the four shapes of writing them again:
 * `describeFailure` (validation codes before the problem's title),
 * `unwrapResource` (`{ resource }` or a bare payload, without a cast through
 * `unknown`), `errorCodeForStatus` (an HTTP status to an error page), and
 * `useViewerChange` ("the viewer changed", which a second tab signing another
 * account in is). Read through the AST, so a copy quoted in a comment is not a
 * violation; `console` arguments and spec files are exempt.
 */
import { describe, it, expect, vi } from "vitest";

// A whole-tree AST scan legitimately outruns the 5s default when the suite
// saturates every core, which is what a coverage run does.
vi.setConfig({ testTimeout: 30_000 });
import { readFileSync } from "fs";
import ts from "typescript";
import { parse as parseSfc } from "vue/compiler-sfc";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

/** The names a problem document travels under in this codebase. */
const ERROR_NAMES = new Set(["error", "apiError", "fetchError", "err", "e"]);

const isSource = (name: string): boolean =>
  (name.endsWith(".ts") || name.endsWith(".vue")) &&
  !name.endsWith(".spec.ts") &&
  !name.endsWith(".d.ts");

const FILES = collect(CLIENT_SRC, isSource);

/** A script, and the line of its file that the script's first line is on. */
interface Script {
  code: string;
  firstLine: number;
}

/**
 * Both script blocks of a single-file component, or the whole file for a
 * module. A component may keep its composable in the plain `<script>` beside
 * the `<script setup>` that calls it, and a scan of one block skipped it.
 */
function scriptsOf(file: string): Script[] {
  const raw = readFileSync(file, "utf8");
  if (!file.endsWith(".vue")) return [{ code: raw, firstLine: 1 }];
  const { descriptor } = parseSfc(raw);
  return [descriptor.script, descriptor.scriptSetup].flatMap((block) =>
    block ? [{ code: block.content, firstLine: block.loc.start.line }] : [],
  );
}

/** Every node of every source, with the file and the line it came from. */
function findAll(
  match: (node: ts.Node) => boolean,
  skip: (path: string) => boolean = () => false,
): string[] {
  const found: string[] = [];
  for (const file of FILES) {
    const path = asPath(file);
    if (skip(path)) continue;
    for (const { code, firstLine } of scriptsOf(file)) {
      const source = ts.createSourceFile(
        file,
        code,
        ts.ScriptTarget.Latest,
        true,
      );
      const visit = (node: ts.Node): void => {
        if (match(node)) {
          const line =
            source.getLineAndCharacterOfPosition(node.getStart()).line +
            firstLine;
          found.push(
            `${path}: ${node.getText().split("\n")[0]} (line ${line})`,
          );
        }
        ts.forEachChild(node, visit);
      };
      visit(source);
    }
  }
  return found;
}

/**
 * The expression inside however many parentheses wrap it: Prettier writes a
 * cast on the right of `??` as `a ?? (b as T)`.
 */
function unparenthesize(node: ts.Expression): ts.Expression {
  let current = node;
  while (ts.isParenthesizedExpression(current)) current = current.expression;
  return current;
}

/** The value a cast chain casts: `(x as unknown as T)` is about `x`. */
function castSubject(node: ts.Expression): ts.Expression {
  let current = unparenthesize(node);
  while (ts.isAsExpression(current)) {
    current = unparenthesize(current.expression);
  }
  return current;
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
        // `x.resource ?? (x as unknown as T)`, and only that: the same payload
        // read twice, once through the envelope and once cast past the
        // compiler. A nullish default of null, or of a value built on the
        // spot — the optimistic topic the forum store falls back to when a
        // mutation answers no body — is a different thing and stays allowed.
        if (node.operatorToken.kind === ts.SyntaxKind.QuestionQuestionToken) {
          const left = unparenthesize(node.left);
          const right = unparenthesize(node.right);
          if (
            ts.isPropertyAccessExpression(left) &&
            left.name.text === "resource" &&
            ts.isAsExpression(right) &&
            castSubject(right).getText() ===
              unparenthesize(left.expression).getText()
          ) {
            return true;
          }
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

    // The home of the SSOT is not exempt, only allowed to hold one map.
    const maps = findAll(
      (node) =>
        (ts.isFunctionDeclaration(node) || ts.isArrowFunction(node)) &&
        node.parameters.some((p) => p.name.getText() === "status") &&
        returnsPageCode(node),
    );
    const home = "shared/ui/ErrorPage/errorConfig.ts:";

    expect(
      maps.filter((site) => !site.startsWith(home)),
      "use errorCodeForStatus from shared/ui/ErrorPage: three copies of this map had already diverged",
    ).toEqual([]);

    expect(
      maps,
      `${home} declares the map more than once; the second copy is the divergence`,
    ).toHaveLength(1);
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
