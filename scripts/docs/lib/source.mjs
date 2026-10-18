// Reading TypeScript and Vue sources: the syntax tree comes from the compiler,
// the split of a .vue file into its script and template parts is textual.
//
// An address in code is rarely one literal. It is a template with variables in
// it, a constant of the module at its head, a field of a class, or the body of a
// small arrow function. textOf() follows exactly those shapes and nothing more:
// what it follows becomes text, any other expression inside a template becomes
// PARAM, and an argument that is not a string at all comes back as null and is
// counted by the caller as unread. choicesOf() adds the one shape where an
// address is one of several: a conditional between literals, directly or in a
// local constant.

/** What an expression the reader cannot evaluate stands for inside an address. */
export const PARAM = "\u0001";

export function parse(ts, fileName, text) {
  return ts.createSourceFile(fileName, text, ts.ScriptTarget.Latest, true, ts.ScriptKind.TS);
}

/** The expression under `as`, `satisfies`, `!` and parentheses. */
export function unwrap(ts, node) {
  let current = node;
  while (
    current &&
    (ts.isAsExpression(current) ||
      ts.isSatisfiesExpression(current) ||
      ts.isParenthesizedExpression(current) ||
      ts.isNonNullExpression(current) ||
      ts.isTypeAssertionExpression(current))
  ) {
    current = current.expression;
  }
  return current;
}

/** Name of a property of an object literal, when it is spelled plainly. */
export function keyOf(ts, property) {
  const name = property.name;
  if (name && (ts.isIdentifier(name) || ts.isStringLiteral(name))) return name.text;
  return undefined;
}

/** Plain `key: value` properties of an object literal, by key. */
export function propertiesOf(ts, object) {
  const found = new Map();
  for (const property of object.properties) {
    if (!ts.isPropertyAssignment(property)) continue;
    const key = keyOf(ts, property);
    if (key !== undefined) found.set(key, property.initializer);
  }
  return found;
}

/** Calls every node of a tree, the root included. */
export function visit(ts, node, callback) {
  callback(node);
  ts.forEachChild(node, (child) => visit(ts, child, callback));
}

function isPlainString(ts, node) {
  return ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node);
}

/**
 * What a file lets an address refer to: string constants at the top level,
 * string fields of classes, and arrow functions with an expression body
 * declared as constants anywhere in the file.
 */
export function scopeOf(ts, sourceFile) {
  const constants = new Map();
  const fields = new Map();
  const arrows = new Map();
  for (const statement of sourceFile.statements) {
    if (!ts.isVariableStatement(statement)) continue;
    if (!(statement.declarationList.flags & ts.NodeFlags.Const)) continue;
    for (const declaration of statement.declarationList.declarations) {
      const value = declaration.initializer && unwrap(ts, declaration.initializer);
      if (ts.isIdentifier(declaration.name) && value && isPlainString(ts, value)) {
        constants.set(declaration.name.text, value.text);
      }
    }
  }
  visit(ts, sourceFile, (node) => {
    if (ts.isPropertyDeclaration(node) && ts.isIdentifier(node.name) && node.initializer) {
      const value = unwrap(ts, node.initializer);
      if (isPlainString(ts, value)) fields.set(node.name.text, value.text);
    }
    if (
      ts.isVariableDeclaration(node) &&
      ts.isIdentifier(node.name) &&
      node.initializer &&
      ts.isArrowFunction(node.initializer) &&
      !ts.isBlock(node.initializer.body)
    ) {
      arrows.set(node.name.text, node.initializer);
    }
  });
  return { constants, fields, arrows, bindings: new Map() };
}

/** A scope with some names bound to fixed text, for reading a function body. */
export function bind(scope, names, value) {
  const bindings = new Map(scope.bindings);
  for (const [index, name] of names.entries()) {
    bindings.set(name, typeof value === "function" ? value(index) : value);
  }
  return { ...scope, bindings };
}

export function parameterNames(ts, fn) {
  return fn.parameters.map((p) => (ts.isIdentifier(p.name) ? p.name.text : ""));
}

/** One expression inside a template: text when it can be followed, PARAM otherwise. */
function substitution(ts, expression, scope) {
  const node = unwrap(ts, expression);
  if (ts.isIdentifier(node)) {
    if (scope.bindings.has(node.text)) return scope.bindings.get(node.text);
    if (scope.constants.has(node.text)) return scope.constants.get(node.text);
    return PARAM;
  }
  if (
    ts.isPropertyAccessExpression(node) &&
    node.expression.kind === ts.SyntaxKind.ThisKeyword &&
    scope.fields.has(node.name.text)
  ) {
    return scope.fields.get(node.name.text);
  }
  return textOf(ts, node, scope) ?? PARAM;
}

function templateText(ts, node, scope) {
  let text = node.head.text;
  for (const span of node.templateSpans) {
    text += substitution(ts, span.expression, scope) + span.literal.text;
  }
  return text;
}

/** `"a" + b`: text when either side is, an unknown side standing as PARAM. */
function concatenation(ts, node, scope) {
  const left = textOf(ts, node.left, scope);
  const right = textOf(ts, node.right, scope);
  if (left === null && right === null) return null;
  return (left ?? PARAM) + (right ?? PARAM);
}

/** The body of a small arrow function of the file, its parameters standing as PARAM. */
function arrowCall(ts, node, scope) {
  const arrow = scope.arrows.get(node.expression.text);
  if (!arrow) return null;
  return textOf(ts, arrow.body, bind(scope, parameterNames(ts, arrow), PARAM));
}

/**
 * The text of an address expression, or null when the expression is not a
 * string the reader can follow.
 */
export function textOf(ts, expression, scope) {
  const node = unwrap(ts, expression);
  if (!node) return null;
  if (isPlainString(ts, node)) return node.text;
  if (ts.isTemplateExpression(node)) return templateText(ts, node, scope);
  if (ts.isIdentifier(node)) {
    if (scope.bindings.has(node.text)) return scope.bindings.get(node.text);
    return scope.constants.get(node.text) ?? null;
  }
  if (ts.isBinaryExpression(node) && node.operatorToken.kind === ts.SyntaxKind.PlusToken) {
    return concatenation(ts, node, scope);
  }
  if (ts.isCallExpression(node) && ts.isIdentifier(node.expression)) return arrowCall(ts, node, scope);
  return null;
}


/**
 * A .vue file cut into what the compiler reads and what it does not. The
 * script blocks are found by their tags, which a single-file component spells
 * one way; the rest is the template, with its HTML comments taken out.
 */
export function vueParts(text) {
  const scripts = [];
  const template = text
    .replace(/<script\b[^>]*>([\s\S]*?)<\/script>/g, (_, body) => {
      scripts.push(body);
      return "";
    })
    .replace(/<style\b[^>]*>[\s\S]*?<\/style>/g, "")
    .replace(/<!--[\s\S]*?-->/g, "");
  return { scripts, template };
}

/**
 * Attributes of a template. The value of an attribute is always written in
 * double quotes in this client and cannot hold one, so the regular expression
 * finds every attribute exactly; a bound value (`:to`, `v-bind:`, `v-*`, `@`)
 * is an expression and is handed back for the compiler to parse.
 */
export function templateAttributes(template) {
  const bound = [];
  const plain = [];
  for (const match of template.matchAll(/\s([:@#]?[\w.:-]+)="([^"]*)"/g)) {
    const [, name, value] = match;
    if (/^(?::|@|#|v-)/.test(name)) bound.push(value);
    else plain.push({ name, value });
  }
  return { bound, plain };
}

/** The initializer of a `const` of that name in a block around the node. */
function localConstant(ts, identifier) {
  for (let block = identifier.parent; block; block = block.parent) {
    if (!ts.isBlock(block) && !ts.isSourceFile(block)) continue;
    for (const statement of block.statements) {
      if (!ts.isVariableStatement(statement)) continue;
      if (!(statement.declarationList.flags & ts.NodeFlags.Const)) continue;
      const found = statement.declarationList.declarations.find(
        (d) => ts.isIdentifier(d.name) && d.name.text === identifier.text && d.initializer,
      );
      if (found) return found.initializer;
    }
  }
  return undefined;
}

/**
 * Every text an expression can take when it picks between addresses:
 * `a ? "x" : "y"`, `a ?? "x"`, or a local constant holding such a choice.
 */
export function choicesOf(ts, expression, scope) {
  const node = unwrap(ts, expression);
  if (!node) return [];
  if (ts.isConditionalExpression(node)) {
    return [...choicesOf(ts, node.whenTrue, scope), ...choicesOf(ts, node.whenFalse, scope)];
  }
  if (
    ts.isBinaryExpression(node) &&
    [ts.SyntaxKind.QuestionQuestionToken, ts.SyntaxKind.BarBarToken].includes(node.operatorToken.kind)
  ) {
    return [...choicesOf(ts, node.left, scope), ...choicesOf(ts, node.right, scope)];
  }
  const text = textOf(ts, node, scope);
  if (text !== null) return [text];
  if (ts.isIdentifier(node)) {
    const initializer = localConstant(ts, node);
    if (initializer) return choicesOf(ts, initializer, scope);
  }
  return [];
}
