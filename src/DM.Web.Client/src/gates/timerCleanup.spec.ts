/**
 * @vitest-environment node
 */

/**
 * A setTimeout, setInterval or requestAnimationFrame that a component or a
 * composable starts is cleared when its owner goes away: the handle is passed
 * to clearTimeout, clearInterval or cancelAnimationFrame inside a function that
 * onScopeDispose, onBeforeUnmount or onUnmounted runs, or inside a cleanup the
 * composable returns to its caller, directly or through local functions they
 * call. A timer left running fires on a page that is gone: a request with no
 * reader, an emit with no listener. Code that lives as long as the site
 * (app/, main.ts, stores, plugins) is out of scope by rule; a one-shot that is
 * harmless after unmount stands in TIMER_REGISTER with its reason.
 */
import { describe, it, expect, vi } from "vitest";
import { readFileSync } from "fs";
import ts from "typescript";
import { parse as parseSfc } from "vue/compiler-sfc";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

// A whole-tree AST scan outruns the 5s default when a coverage run saturates
// every core.
vi.setConfig({ testTimeout: 60_000 });

const STARTS: Record<string, string> = {
  setTimeout: "clearTimeout",
  setInterval: "clearInterval",
  requestAnimationFrame: "cancelAnimationFrame",
};
const CLEARS = new Set(Object.values(STARTS));
const DISPOSALS = new Set(["onScopeDispose", "onBeforeUnmount", "onUnmounted"]);

/** Code that lives as long as the site: it has no owner to go away. */
const SITE_LIFETIME = (path: string): boolean =>
  path.startsWith("app/") ||
  path === "main.ts" ||
  path.startsWith("shared/lib/testing/") ||
  /(^|\/)(stores?|plugins)\//.test(path) ||
  /(^|\/)store\.ts$|Store\.ts$/.test(path);

/**
 * Files whose timers are not cleared on unmount, with how many there are and
 * why that is harmless. The count is exact both ways: a new uncleared timer in
 * a listed file turns red as well as one that was cleared since.
 */
const TIMER_REGISTER: Record<string, { timers: number; why: string }> = {
  "pages/forum/usePinnedDrag.ts": {
    timers: 1,
    why: "a 0 ms one-shot that only resets a flag on a plain object nothing reads after unmount",
  },
  "pages/game/room/useRoomOpening.ts": {
    timers: 1,
    why: "a single awaited frame; view.gone, set in onScopeDispose, is checked right after it",
  },
  "pages/moderation/ModerationBans.vue": {
    timers: 1,
    why: "a one-shot 100 ms wait in a watcher; onWatcherCleanup sets stopped, checked right after the wait",
  },
  "widgets/chat-feed/useChatHistoryWindow.ts": {
    timers: 1,
    why: "a one-shot whose callback goes through the feed ref, null once ChatFeed unmounts",
  },
  "widgets/chat-feed/useFeedPosition.ts": {
    timers: 1,
    why: "the 100 ms arrival pulse, a one-shot that returns at once when the list ref is null after unmount",
  },
  "shared/lib/composables/useScrollToElement.ts": {
    timers: 2,
    why: "a one-shot class removal on an element that is detached after unmount, and an awaited 100 ms settle followed by a stopped() check",
  },
  "shared/lib/composables/useBotTrap.ts": {
    timers: 1,
    why: "a wait inside a submit the person started; clearing it would leave the promise pending and the request unsent",
  },
  "shared/lib/composables/createFilterDispatcher.ts": {
    timers: 2,
    why: "the dispatcher lives as long as the site and drops the pending state when the route path changed; clearing on unmount would lose a filter change when the board remounts",
  },
  "shared/lib/composables/useAddressPing.ts": {
    timers: 2,
    why: "two abort timers of a request, each cleared when its request settles; onUnmounted aborts the round, so the requests settle",
  },
  "shared/lib/bbcode/bbcodePrivateRecipients.ts": {
    timers: 1,
    why: "the hide timer of the one module-level recipients popup, which lives as long as the site",
  },
  "shared/lib/errors/generalPlate.ts": {
    timers: 1,
    why: "the site-wide error plate lives as long as the site",
  },
  "shared/lib/utils/deferredRead.ts": {
    timers: 1,
    why: "the debounce of a read whose callers are stores that live as long as the site",
  },
  "shared/lib/composables/useToast.ts": {
    timers: 1,
    why: "the toast queue is module state that lives as long as the site",
  },
};

const isSource = (name: string): boolean =>
  (name.endsWith(".ts") || name.endsWith(".vue")) &&
  !name.endsWith(".spec.ts") &&
  !name.endsWith(".d.ts");

/** Both script blocks of a component, or the whole module. */
function scriptsOf(file: string): string[] {
  const raw = readFileSync(file, "utf8");
  if (!file.endsWith(".vue")) return [raw];
  const { descriptor } = parseSfc(raw);
  return [descriptor.script, descriptor.scriptSetup].flatMap((block) =>
    block ? [block.content] : [],
  );
}

const calleeName = (node: ts.CallExpression): string | undefined =>
  ts.isIdentifier(node.expression)
    ? node.expression.text
    : ts.isPropertyAccessExpression(node.expression) &&
        ts.isIdentifier(node.expression.expression) &&
        node.expression.expression.text === "window"
      ? node.expression.name.text
      : undefined;

const isFunction = (node: ts.Node): node is ts.FunctionLikeDeclaration =>
  ts.isFunctionDeclaration(node) ||
  ts.isFunctionExpression(node) ||
  ts.isArrowFunction(node) ||
  ts.isMethodDeclaration(node);

/** The declaration of a name in one scope: a parameter, a local, a function. */
function declaredIn(scope: ts.Node, name: string): ts.Node | undefined {
  const named = (node: ts.Node): boolean =>
    (ts.isVariableDeclaration(node) ||
      ts.isParameter(node) ||
      ts.isFunctionDeclaration(node)) &&
    !!node.name &&
    ts.isIdentifier(node.name) &&
    node.name.text === name;
  if (isFunction(scope)) return scope.parameters.find(named);
  if (ts.isBlock(scope) || ts.isSourceFile(scope))
    return scope.statements
      .flatMap((one): ts.Node[] =>
        ts.isVariableStatement(one)
          ? [...one.declarationList.declarations]
          : [one],
      )
      .find(named);
  const loop =
    ts.isForOfStatement(scope) || ts.isForStatement(scope)
      ? scope.initializer
      : undefined;
  return loop && ts.isVariableDeclarationList(loop)
    ? loop.declarations.find(named)
    : undefined;
}

type Key = ts.Node | string;

/** What a name stands for, so two locals called `timer` stay apart. */
function declarationOf(id: ts.Identifier): Key {
  for (let scope: ts.Node = id.parent; scope; scope = scope.parent) {
    const found = declaredIn(scope, id.text);
    if (found) return found;
  }
  return `global:${id.text}`;
}

const stripped = (node: ts.Expression): ts.Expression =>
  ts.isNonNullExpression(node) || ts.isParenthesizedExpression(node)
    ? stripped(node.expression)
    : node;

const ids = new Map<ts.Node, number>();
const idOf = (node: ts.Node): number => {
  if (!ids.has(node)) ids.set(node, ids.size + 1);
  return ids.get(node)!;
};

/** The member path read past `!` and parentheses, so `tail!.timer` and `tail.timer` are one handle. */
function pathOf(node: ts.Expression): string {
  const plain = stripped(node);
  if (ts.isPropertyAccessExpression(plain))
    return `${pathOf(plain.expression)}.${plain.name.text}`;
  if (ts.isElementAccessExpression(plain))
    return `${pathOf(plain.expression)}[${plain.argumentExpression.getText()}]`;
  return "";
}

/**
 * Where a handle lives: a name by its declaration, a member by the
 * declaration of its root and its path. A parameter root stands for whatever
 * the caller passes, so its type joins the key, as `tail.timer` of a
 * LiveTail is one handle in the function that arms it and the one that
 * clears; an untyped parameter keys by its own function.
 */
function keyOf(node: ts.Expression): Key | undefined {
  let root = stripped(node);
  while (
    ts.isPropertyAccessExpression(root) ||
    ts.isElementAccessExpression(root)
  )
    root = stripped(root.expression);
  if (!ts.isIdentifier(root)) return undefined;
  const declaration = declarationOf(root);
  if (root === stripped(node)) return declaration;
  const path = pathOf(node);
  if (typeof declaration === "string") return `${declaration}${path}`;
  if (!ts.isParameter(declaration)) return `${idOf(declaration)}${path}`;
  const owner = declaration.type
    ? `type:${declaration.type.getText()}`
    : idOf(declaration.parent);
  return `${owner}${path}`;
}

/** Where a timer's handle is kept, or undefined when nothing keeps it. */
function handleOf(call: ts.CallExpression): Key | undefined {
  const parent = call.parent;
  if (ts.isVariableDeclaration(parent) && ts.isIdentifier(parent.name))
    return parent;
  if (
    ts.isBinaryExpression(parent) &&
    parent.operatorToken.kind === ts.SyntaxKind.EqualsToken &&
    parent.right === call
  )
    return keyOf(parent.left);
  return undefined;
}

/**
 * The handles a clear's argument names, with what an alias or a loop
 * variable of it stands for: `const timer = timers[id]` and
 * `for (const timer of [showTimer, hideTimer])`.
 */
function keysIn(node: ts.Node, into: Set<Key>, depth = 0): void {
  const visit = (child: ts.Node): void => {
    const isName =
      ts.isPropertyAccessExpression(child.parent) &&
      child.parent.name === child;
    if (ts.isIdentifier(child) && !isName) {
      const declaration = declarationOf(child);
      into.add(declaration);
      if (depth === 0 && typeof declaration !== "string") {
        const source = ts.isVariableDeclaration(declaration)
          ? ts.isForOfStatement(declaration.parent.parent)
            ? declaration.parent.parent.expression
            : declaration.initializer
          : undefined;
        if (source) keysIn(source, into, 1);
      }
    }
    if (
      ts.isPropertyAccessExpression(child) ||
      ts.isElementAccessExpression(child)
    ) {
      const key = keyOf(child);
      if (key) into.add(key);
    }
    ts.forEachChild(child, visit);
  };
  visit(node);
}

/** What a script names and starts, and the functions a disposal runs. */
interface ScriptIndex {
  named: Map<string, ts.Node[]>;
  starts: ts.CallExpression[];
  roots: { node: ts.Node; returned: boolean }[];
}

const isAssignment = (node: ts.Node): node is ts.BinaryExpression =>
  ts.isBinaryExpression(node) &&
  node.operatorToken.kind === ts.SyntaxKind.EqualsToken;

/** The name a node gives a value under, with the value. */
function namingOf(node: ts.Node): [string, ts.Node] | undefined {
  if (ts.isFunctionDeclaration(node) && node.name)
    return [node.name.text, node];
  if (
    ts.isVariableDeclaration(node) &&
    ts.isIdentifier(node.name) &&
    node.initializer
  )
    return [node.name.text, node.initializer];
  if (isAssignment(node) && ts.isIdentifier(node.left))
    return [node.left.text, node.right];
  return undefined;
}

/** What a member handed back is called when the caller runs it to let go. */
const CLEANUP_NAMES = new Set([
  "stop",
  "dispose",
  "cancel",
  "clear",
  "unsubscribe",
  "disconnect",
]);

/**
 * The cleanups a return hands back: the value itself when it is returned
 * alone, or the members of an object under a cleanup name. Any other member
 * is a read or an action the caller may never run when the owner goes.
 */
function returnedCleanups(node: ts.ReturnStatement): ts.Node[] {
  const value = node.expression && stripped(node.expression);
  if (!value) return [];
  if (!ts.isObjectLiteralExpression(value)) return [value];
  return value.properties.flatMap((one): ts.Node[] => {
    const name =
      one.name && (ts.isIdentifier(one.name) || ts.isStringLiteral(one.name))
        ? one.name.text
        : undefined;
    if (!name || !CLEANUP_NAMES.has(name)) return [];
    if (ts.isPropertyAssignment(one)) return [one.initializer];
    if (ts.isShorthandPropertyAssignment(one)) return [one.name];
    return [one];
  });
}

/** True when a return leaves a function declared at the top of the module. */
function returnsFromTopLevel(node: ts.ReturnStatement): boolean {
  let owner: ts.Node = node.parent;
  while (!ts.isFunctionLike(owner)) owner = owner.parent;
  const holder = ts.isVariableDeclaration(owner.parent)
    ? owner.parent.parent.parent
    : owner;
  return ts.isSourceFile(holder.parent);
}

function indexScript(source: ts.SourceFile): ScriptIndex {
  // Every function under every name it travels by, shadowing ignored.
  const index: ScriptIndex = { named: new Map(), starts: [], roots: [] };
  const visit = (node: ts.Node): void => {
    const naming = namingOf(node);
    if (naming)
      index.named.set(naming[0], [
        ...(index.named.get(naming[0]) ?? []),
        naming[1],
      ]);
    const callee = ts.isCallExpression(node) ? calleeName(node) : undefined;
    if (callee && callee in STARTS)
      index.starts.push(node as ts.CallExpression);
    const disposed = (node as ts.CallExpression).arguments?.[0];
    if (callee && DISPOSALS.has(callee) && disposed)
      index.roots.push({ node: disposed, returned: false });
    // A cleanup handed back to the caller of a module-level function, which
    // must clear without arming.
    if (ts.isReturnStatement(node) && returnsFromTopLevel(node))
      for (const value of returnedCleanups(node))
        index.roots.push({ node: value, returned: true });
    ts.forEachChild(node, visit);
  };
  visit(source);
  return index;
}

function armsTimer(node: ts.Node): boolean {
  if (ts.isCallExpression(node) && (calleeName(node) ?? "") in STARTS)
    return true;
  return ts.forEachChild(node, armsTimer) ?? false;
}
/** What a call inside reachable code runs next, by name. */
function calledName(node: ts.CallExpression): ts.Identifier | undefined {
  if (ts.isIdentifier(node.expression)) return node.expression;
  // `letPageGo?.()` and `handle.stop()` run what the name holds.
  if (
    ts.isPropertyAccessExpression(node.expression) &&
    ts.isIdentifier(node.expression.expression)
  )
    return node.expression.expression;
  return undefined;
}

/** Every handle a clear reachable from a disposal is passed. */
function clearedHandles({ named, roots }: ScriptIndex): Set<Key> {
  const cleared = new Set<Key>();
  const seen = new Set<ts.Node>();
  const reach = (node: ts.Node, returned: boolean): void => {
    if (seen.has(node)) return;
    seen.add(node);
    if (ts.isIdentifier(node)) {
      for (const target of named.get(node.text) ?? []) reach(target, returned);
      return;
    }
    if (!isFunction(node) || (returned && armsTimer(node))) return;
    const walk = (child: ts.Node): void => {
      if (ts.isCallExpression(child)) clearOrFollow(child, returned);
      ts.forEachChild(child, walk);
    };
    walk(node);
  };
  const clearOrFollow = (call: ts.CallExpression, returned: boolean): void => {
    const callee = calleeName(call);
    if (callee && CLEARS.has(callee) && call.arguments[0]) {
      keysIn(call.arguments[0], cleared);
      return;
    }
    const next = calledName(call);
    if (next) reach(next, returned);
    // An inline callback runs; a named one only through forEach, since
    // `removeEventListener("focus", check)` hands a function on unrun.
    const iterates =
      ts.isPropertyAccessExpression(call.expression) &&
      call.expression.name.text === "forEach";
    for (const argument of call.arguments)
      if (isFunction(argument) || (iterates && ts.isIdentifier(argument)))
        reach(argument, returned);
  };
  for (const { node, returned } of roots) reach(node, returned);
  return cleared;
}

/** The timers of one script that no disposal clears, by their first line. */
function unclearedIn(code: string): string[] {
  const source = ts.createSourceFile(
    "x.ts",
    code,
    ts.ScriptTarget.Latest,
    true,
  );
  const index = indexScript(source);
  const cleared = clearedHandles(index);
  return index.starts
    .filter((call) => {
      const handle = handleOf(call);
      return !handle || !cleared.has(handle);
    })
    .map((call) => call.getText().split("\n")[0]);
}

/** Every uncleared timer of the given files, path to line texts. */
function unclearedTimers(files: string[]): Map<string, string[]> {
  const found = new Map<string, string[]>();
  for (const file of files) {
    const path = asPath(file);
    if (SITE_LIFETIME(path)) continue;
    const lines = scriptsOf(file).flatMap(unclearedIn);
    if (lines.length) found.set(path, lines);
  }
  return found;
}

const FILES = collect(CLIENT_SRC, isSource);

describe("a timer of a component or a composable", () => {
  it("is cleared when its owner goes away, or registered with a reason", () => {
    const offenders: string[] = [];
    for (const [path, lines] of unclearedTimers(FILES)) {
      const entry = TIMER_REGISTER[path];
      if (entry?.timers === lines.length) continue;
      offenders.push(
        `${path}: ${lines.length} timer(s) not cleared on unmount (${lines.join(" | ")}) — keep the handle and clear it in onScopeDispose, onBeforeUnmount or onUnmounted; a one-shot harmless after unmount goes into TIMER_REGISTER with its count and reason`,
      );
    }
    expect(offenders).toEqual([]);
  });

  it("keeps no reason for a timer that is gone or cleared", () => {
    const found = unclearedTimers(FILES);
    const stale = Object.entries(TIMER_REGISTER)
      .filter(([path, { timers }]) => (found.get(path)?.length ?? 0) < timers)
      .map(
        ([path]) =>
          `${path}: fewer uncleared timers than registered, lower or drop the entry`,
      );
    expect(stale).toEqual([]);
  });

  it("reads the timers of the tree", () => {
    // A walk that found nothing passes the rule for the wrong reason.
    let total = 0;
    for (const file of FILES)
      for (const code of scriptsOf(file))
        total += (
          code.match(/\b(setTimeout|setInterval|requestAnimationFrame)\(/g) ??
          []
        ).length;
    expect(total).toBeGreaterThan(30);
  });
});
