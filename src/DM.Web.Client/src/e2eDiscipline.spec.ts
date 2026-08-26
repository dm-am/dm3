/**
 * @vitest-environment node
 */

/**
 * Three rules over the Playwright corpus, all written after the same failure: a
 * tier that reported green because it had not run.
 *
 * A test that skips itself is the mechanism. `test.skip(condition)` in a body,
 * or in a describe — where Playwright evaluates it while collecting the file,
 * long before any hook has run — turns an unmet precondition into a pass.
 * Fifteen of them across four files were covering a route that matched no
 * action, an id read past the response envelope, and a setup whose errors were
 * caught and logged. A declared skip is a different thing and stays allowed:
 * `test.skip("title", fn)` names a test that is switched off, and the report
 * says so.
 *
 * An assertion is the second half of the same rule. A body that ends on a
 * visibility guard, or on a comment describing what would be true, passes
 * whatever the page renders: eleven tests could not tell a working screen from
 * a white one, and the report counted them. The assertion also has to be one
 * that runs. An `expect` reached only through an `if`, only from inside a
 * `try` that swallows it, or only once per element of a list that can be empty
 * (a `for` over that list, or a callback handed to `forEach`, `map` and their
 * kin) is a word this rule can find and a promise the run does not make. No
 * test in the corpus leans on one today, and this is what keeps it that way.
 *
 * Credentials are the third. The corpus once signed in as
 * alice@example.com, an account the seeder does not create, and then asserted
 * against a signed-out page. Accounts live in e2e/fixtures/auth.ts and nowhere
 * else, and the fixture's defaults have to be accounts DM.Tools.Seeder writes —
 * so this file reads both and compares them, the way eslint.config.js reads the
 * global component registration instead of restating it.
 *
 * The check is over the AST rather than over the text: a `test.skip` quoted in
 * a comment (the fixture's own history is written in one) and an address named
 * in prose are not violations.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative, resolve } from "path";
import { fileURLToPath } from "url";
import ts from "typescript";

const HERE = dirname(fileURLToPath(import.meta.url));
// src -> DM.Web.Client
const CLIENT_ROOT = resolve(HERE, "..");
const REPO_ROOT = resolve(CLIENT_ROOT, "..", "..");

const E2E_ROOT = join(CLIENT_ROOT, "e2e");
const AUTH_FIXTURE = join(E2E_ROOT, "fixtures", "auth.ts");
const USER_SEEDER = join(
  REPO_ROOT,
  "src",
  "DM.Tools.Seeder",
  "Seeding",
  "DataSeeder.Users.cs",
);

const collect = (dir: string): string[] => {
  const found: string[] = [];
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) found.push(...collect(full));
    else if (name.endsWith(".ts")) found.push(full);
  }
  return found;
};

const parse = (file: string): ts.SourceFile =>
  ts.createSourceFile(
    file,
    readFileSync(file, "utf8"),
    ts.ScriptTarget.Latest,
    true,
  );

const at = (node: ts.Node): string => {
  const source = node.getSourceFile();
  const { line, character } = source.getLineAndCharacterOfPosition(
    node.getStart(),
  );
  const path = relative(REPO_ROOT, source.fileName).split("\\").join("/");
  return `${path}:${line + 1}:${character + 1}`;
};

const walk = (node: ts.Node, visit: (node: ts.Node) => void): void => {
  visit(node);
  node.forEachChild((child) => walk(child, visit));
};

/** Dotted name of a call target: `test.skip`, `test.describe.skip`, ... */
const calleeName = (expression: ts.Expression): string | null => {
  if (ts.isIdentifier(expression)) return expression.text;
  if (ts.isPropertyAccessExpression(expression)) {
    const head = calleeName(expression.expression);
    return head === null ? null : `${head}.${expression.name.text}`;
  }
  return null;
};

const SKIPPING = /^(test|describe)(\.\w+)*\.(skip|fixme)$/;
const ADDRESS = /^[^\s@]+@[^\s@]+\.[a-z]{2,}$/i;

const isPlainString = (
  node: ts.Node,
): node is ts.StringLiteral | ts.NoSubstitutionTemplateLiteral =>
  ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node);

/**
 * `test.skip("title", fn)` declares a switched-off test. Every other shape —
 * no argument at all, a condition, a callback over the fixtures — decides at
 * run time whether to run, and that is the shape that hides a failure.
 */
const isDeclaration = (call: ts.CallExpression): boolean =>
  call.arguments.length >= 2 && isPlainString(call.arguments[0]);

/**
 * Array methods that call their argument once per element, so a callback handed
 * to one of them does not run at all over an empty list - the same promise a
 * `for` over that list makes, and the same non-promise.
 */
const PER_ELEMENT = new Set([
  "forEach",
  "map",
  "flatMap",
  "filter",
  "find",
  "findIndex",
  "findLast",
  "findLastIndex",
  "some",
  "every",
  "reduce",
  "reduceRight",
]);

/**
 * Whether the parent decides that the child runs at all: a branch, a switch, the
 * right side of a short circuit, a loop, the callback of a list method, and a
 * try, which catches the failure instead of reporting it.
 */
const gates = (parent: ts.Node, child: ts.Node): boolean => {
  if (ts.isIfStatement(parent)) return child !== parent.expression;
  if (ts.isConditionalExpression(parent)) return child !== parent.condition;
  if (ts.isBinaryExpression(parent)) {
    const operator = parent.operatorToken.kind;
    return (
      child === parent.right &&
      (operator === ts.SyntaxKind.AmpersandAmpersandToken ||
        operator === ts.SyntaxKind.BarBarToken ||
        operator === ts.SyntaxKind.QuestionQuestionToken)
    );
  }
  if (
    ts.isCallExpression(parent) &&
    ts.isPropertyAccessExpression(parent.expression) &&
    PER_ELEMENT.has(parent.expression.name.text)
  ) {
    // The arguments only: `items.forEach` is reached whatever `items` holds, and
    // `expect(items.map(...))` asserts before the callback is anybody's problem.
    return parent.arguments.some((argument) => argument === child);
  }
  return (
    ts.isForStatement(parent) ||
    ts.isForOfStatement(parent) ||
    ts.isForInStatement(parent) ||
    ts.isWhileStatement(parent) ||
    ts.isDoStatement(parent) ||
    ts.isSwitchStatement(parent) ||
    ts.isTryStatement(parent) ||
    ts.isCatchClause(parent)
  );
};

/** Whether the body asserts on the path it takes every time. */
const assertsUnconditionally = (body: ts.Node): boolean => {
  let asserts = false;
  const visit = (node: ts.Node, guarded: boolean): void => {
    if (!guarded && ts.isCallExpression(node)) {
      const called = calleeName(node.expression);
      if (called === "expect" || called?.startsWith("expect.")) asserts = true;
    }
    node.forEachChild((child) => visit(child, guarded || gates(node, child)));
  };
  visit(body, false);
  return asserts;
};

/** Defaults of `process.env.X || "value"` in one exported account object. */
const fixtureAccount = (
  source: ts.SourceFile,
  name: string,
): Record<string, string> => {
  const account: Record<string, string> = {};
  walk(source, (node) => {
    if (
      !ts.isVariableDeclaration(node) ||
      !ts.isIdentifier(node.name) ||
      node.name.text !== name ||
      node.initializer === undefined ||
      !ts.isObjectLiteralExpression(node.initializer)
    ) {
      return;
    }
    for (const property of node.initializer.properties) {
      if (!ts.isPropertyAssignment(property)) continue;
      const value = property.initializer;
      if (
        !ts.isBinaryExpression(value) ||
        value.operatorToken.kind !== ts.SyntaxKind.BarBarToken ||
        !isPlainString(value.right)
      ) {
        continue;
      }
      account[property.name.getText()] = value.right.text;
    }
  });
  return account;
};

describe("no e2e test can hide a failure behind a skip", () => {
  const files = collect(E2E_ROOT);

  it("has a corpus to check", () => {
    // A walk that finds nothing would pass silently.
    expect(files.length).toBeGreaterThan(20);
  });

  it("decides nothing about skipping at run time", () => {
    const violations: string[] = [];
    for (const file of files) {
      walk(parse(file), (node) => {
        if (!ts.isCallExpression(node)) return;
        const name = calleeName(node.expression);
        if (name === null || !SKIPPING.test(name)) return;
        if (isDeclaration(node)) return;
        violations.push(`${at(node)}: ${name}(...)`);
      });
    }
    expect(violations).toEqual([]);
  });

  it("puts an assertion that runs in every test it declares", () => {
    const violations: string[] = [];
    for (const file of collect(join(E2E_ROOT, "tests"))) {
      walk(parse(file), (node) => {
        if (!ts.isCallExpression(node)) return;
        const name = calleeName(node.expression);
        // Dotted names are the declared skips, and a switched-off test owes
        // nothing: the report says it did not run.
        if (name !== "test" && name !== "it") return;
        if (!isDeclaration(node)) return;
        if (assertsUnconditionally(node.arguments[1])) return;

        const title = node.arguments[0] as ts.StringLiteral;
        violations.push(`${at(node)}: ${title.text}`);
      });
    }
    expect(
      violations,
      "assert first, then branch: an expect the run may step over reports the " +
        "same green as one that held",
    ).toEqual([]);
  });

  it("takes account addresses from the fixture", () => {
    const violations: string[] = [];
    for (const file of collect(join(E2E_ROOT, "tests"))) {
      walk(parse(file), (node) => {
        if (!isPlainString(node) || !ADDRESS.test(node.text)) return;
        violations.push(`${at(node)}: ${node.text}`);
      });
    }
    expect(violations).toEqual([]);
  });
});

describe("the e2e accounts are accounts the seeder writes", () => {
  const seeder = readFileSync(USER_SEEDER, "utf8");
  const logins = [...seeder.matchAll(/Login = "([^"]+)"/g)].map(([, v]) => v);
  const addresses = [...seeder.matchAll(/Email = "([^"]+)"/g)].map(([, v]) =>
    v.toLowerCase(),
  );
  const password = seeder.match(/defaultPassword = "([^"]+)"/)?.[1];

  it("reads the seeder", () => {
    // Same reason as above: a regex that matched nothing would pass silently.
    expect(logins.length).toBeGreaterThan(10);
    expect(addresses.length).toBeGreaterThan(10);
    expect(password).toBeTruthy();
  });

  it("holds for every account the fixture declares", () => {
    const fixture = parse(AUTH_FIXTURE);
    for (const name of ["primaryUser", "secondaryUser"]) {
      const account = fixtureAccount(fixture, name);
      expect(Object.keys(account).sort(), name).toEqual([
        "email",
        "password",
        "username",
      ]);
      expect(logins, name).toContain(account.username);
      expect(addresses, name).toContain(account.email.toLowerCase());
      expect(account.password, name).toBe(password);
    }
  });
});

/**
 * The rule above reads a corpus that satisfies it, so nothing in the run tells
 * the two readings of "has an assertion" apart. These do.
 */
describe("the assertion rule counts only assertions that run", () => {
  const bodyOfTheTest = (source: string): ts.Node => {
    const bodies: ts.Node[] = [];
    walk(
      ts.createSourceFile("snippet.ts", source, ts.ScriptTarget.Latest, true),
      (node) => {
        if (!ts.isCallExpression(node)) return;
        if (calleeName(node.expression) !== "test") return;
        if (isDeclaration(node)) bodies.push(node.arguments[1]);
      },
    );
    expect(bodies).toHaveLength(1);
    return bodies[0];
  };

  it("counts one the body reaches on its own path", () => {
    expect(
      assertsUnconditionally(
        bodyOfTheTest('test("t", async () => { expect(one).toBe(two); });'),
      ),
    ).toBe(true);
  });

  it("does not count one reached only through a condition", () => {
    expect(
      assertsUnconditionally(
        bodyOfTheTest('test("t", async () => { if (a) expect(a).toBe(b); });'),
      ),
    ).toBe(false);
  });

  it("does not count one made only inside a loop", () => {
    expect(
      assertsUnconditionally(
        bodyOfTheTest(
          'test("t", async () => { for (const a of all) expect(a).toBe(b); });',
        ),
      ),
    ).toBe(false);
  });

  it("does not count one made only in a callback over a list", () => {
    expect(
      assertsUnconditionally(
        bodyOfTheTest(
          'test("t", async () => { all.forEach((a) => expect(a).toBe(b)); });',
        ),
      ),
    ).toBe(false);
  });

  it("does not count one a catch would swallow", () => {
    expect(
      assertsUnconditionally(
        bodyOfTheTest(
          'test("t", async () => { try { expect(a).toBe(b); } catch {} });',
        ),
      ),
    ).toBe(false);
  });

  it("counts one made on a list the body built, not walked", () => {
    expect(
      assertsUnconditionally(
        bodyOfTheTest(
          'test("t", async () => { expect(all.map((a) => a.id)).toEqual(ids); });',
        ),
      ),
    ).toBe(true);
  });
});
