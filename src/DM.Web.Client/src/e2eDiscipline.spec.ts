/**
 * @vitest-environment node
 */

/**
 * Two rules over the Playwright corpus, both written after the same failure: a
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
 * Credentials are the other half. The corpus once signed in as
 * alice@example.com, an account the seeder does not create, and then asserted
 * against a signed-out page. Accounts live in e2e/fixtures/auth.ts and nowhere
 * else, and the fixture's defaults have to be accounts DM.Tools.Seeder writes —
 * so this file reads both and compares them, the way .eslintrc.cjs reads the
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
