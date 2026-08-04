/**
 * @vitest-environment node
 */

/**
 * The TypeScript models in this folder are hand-written mirrors of C# DTOs,
 * and nothing held the two sides together. They drifted, twice in ways that
 * reached users: five list endpoints promised a bare array and put an envelope
 * on the wire, and the notepad entry declared a `notepadType` union whose
 * members the server has never sent.
 *
 * The server side of the handshake is
 * DM.Web.API.IntegrationTests OpenApiContractShould, which reduces the
 * published schemas to name-to-properties and commits the result as
 * artifacts/openapi-contract.json. This test reads that file and holds the
 * hand-written interfaces to it.
 *
 * Only property NAMES are compared. Types are not: the client legitimately
 * declares its own (string for a uuid, a string union for an enum), and a
 * name-level check already catches the drift that actually happened.
 *
 * Which types are mirrors is decided by their name, not by a list. A schema id
 * is a fully qualified .NET type name, and a client type whose name equals its
 * last segment mirrors it and is held to it. That is what the codebase already
 * does, and it is the only rule under which a mirror written today is checked
 * today: the pairing table this replaces named thirteen types, nothing said
 * which ones were missing, and the other hundred and forty-odd were held to
 * nothing at all.
 *
 * The rule costs one thing — a view model may not borrow the name of a
 * published DTO — and that is the answer worth having, because two shapes
 * under one name is how a DTO comes to be read as a view model in the first
 * place.
 */
import { describe, it, expect } from "vitest";
import { readFileSync, existsSync, readdirSync, statSync } from "fs";
import { dirname, join, relative, resolve } from "path";
import { fileURLToPath } from "url";
import ts from "typescript";

const HERE = dirname(fileURLToPath(import.meta.url));
// models -> api -> shared -> src
const SOURCE_ROOT = resolve(HERE, "..", "..", "..");
// src -> DM.Web.Client -> src -> repository root
const REPO_ROOT = resolve(SOURCE_ROOT, "..", "..", "..");
const SNAPSHOT = join(REPO_ROOT, "artifacts", "openapi-contract.json");

/**
 * Mirrors whose name does not name their schema.
 *
 * Two kinds, and no third. A rename, where the API publishes a list DTO and a
 * detail DTO and the client keeps a single interface for the richer one; and a
 * union, where one client interface stands for several server shapes and is
 * held to all of them at once. Anything else needs no entry — name the type
 * after the DTO and the pairing follows — and an entry that only repeats what
 * the name already says is a test failure, not a harmless extra line.
 */
const MIRRORS: Record<string, string[]> = {
  Game: ["DM.Web.API.Features.Game.Games.GameDetails"],
  Character: ["DM.Web.API.Features.Game.Characters.CharacterDetails"],
  NotepadEntry: ["DM.Web.API.Features.Personal.Notepads.NotepadEntryResponse"],

  // The account's own view of its username change. The client named the type
  // after the request it sends, the server after the response it returns, and
  // the moderation slice has a different type under the same name — which the
  // name rule handles on its own, because that one does match its schema.
  UsernameChangeRequest: [
    "DM.Web.API.Features.Account.Credentials.UsernameChangeResponse",
  ],

  // One interface over three server shapes, every extra field optional and
  // commented as such. Held to the union of the three: a field that none of
  // them sends is still a field that reads undefined.
  User: [
    "DM.Web.API.Features.Community.Users.User",
    "DM.Web.API.Features.Community.Users.UserProfile",
    "DM.Web.API.Features.Personal.Profiles.PersonalProfile",
  ],
};

/**
 * Properties a client model owns outright, per interface.
 *
 * The point of this test is that a field the client reads is a field the
 * server sends. A few are genuinely local — optimistic state the store writes
 * after a mutation — and those have to be named here rather than left to be
 * inferred, so that adding one is a decision somebody made and not a typo that
 * quietly widened the exception.
 */
const CLIENT_ONLY: Record<string, string[]> = {
  // Set after a successful delete so the entry becomes a placeholder instead
  // of vanishing under the reader. No endpoint returns a removed comment, and
  // a reload drops it entirely.
  Comment: ["isRemoved"],
};

/**
 * Fields a mirror declares that no schema of its own sends.
 *
 * Not the same thing as CLIENT_ONLY. These are not owned by the client, they
 * are wrong: each one reads undefined at runtime, and each one was invisible
 * for as long as the pairing was a hand-kept list of thirteen. They are
 * recorded instead of deleted because deleting them is a product decision this
 * file cannot make — the server grows the field, the screen stops asking for
 * it, or the mirror is renamed to the DTO it actually describes — and a suite
 * left red in the meantime is a suite that gets switched off.
 *
 * Checked from both ends, so the list cannot rot into a licence: an entry for
 * a field the model no longer declares fails, and so does an entry for a field
 * the server has started sending.
 */
const UNSERVED: Record<string, string[]> = {
  // A roll on the wire is `rolls` dice of `edges` sides with a `results`
  // array; the mirror describes one die and one number, and the post renders
  // "dundefined: undefined = NaN".
  DiceRoll: ["id", "dice", "result"],

  // `settings` is published by no user schema at all — preferences are their
  // own endpoint — so theme and page size fall back to their defaults for
  // everyone. `birthdayDate` is `birthday` on the wire and an object there,
  // and `registrationUtc` is the dead half of every
  // `registeredUtc ?? registrationUtc`.
  User: ["settings", "birthdayDate", "registrationUtc"],

  // Rating visibility is a profile setting (VisibilitySettings.ShowRating) and
  // the server answers it by sending a null rating, not a flag; the profile
  // reads `rating?.isEnabled` and therefore never shows one.
  Rating: ["isEnabled", "totalRating"],

  // The wire sends the old name and the date. The profile keys its list on
  // `entry.id`, which is undefined for every entry.
  UsernameHistoryEntry: ["id", "newUsername", "approvedByUsername"],

  // BbText serialises to a plain string through BbConverter, exactly as
  // PostBbText on this side already documents; the object with `source` and
  // `html` describes nothing that is ever sent.
  InfoBbText: ["source", "html"],
};

/** A named object type declared somewhere in the client. */
type Declaration = {
  name: string;
  file: string;
  own: Set<string>;
  heritage: string[];
};

/** Every TypeScript source under src/, specs and ambient declarations aside. */
function collectFiles(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      collectFiles(full, out);
    } else if (
      full.endsWith(".ts") &&
      !full.endsWith(".spec.ts") &&
      !full.endsWith(".d.ts")
    ) {
      out.push(full);
    }
  }
  return out;
}

/**
 * Every declaration site, not the first one under a given name.
 *
 * Slices re-declare shared names — `export type Comment = BaseComment` — and
 * two slices can hold genuinely different types under one name. Taking the
 * first and dropping the rest would leave whichever the walk reached second
 * unchecked, which is the failure this file exists to prevent.
 */
function readDeclarations(): Declaration[] {
  const found: Declaration[] = [];

  for (const file of collectFiles(SOURCE_ROOT)) {
    const source = ts.createSourceFile(
      file,
      readFileSync(file, "utf8"),
      ts.ScriptTarget.Latest,
      true,
    );

    // Both shapes are in use: `interface X extends Y` and
    // `type X = { ... }` / `type X = Y & { ... }`.
    const membersOf = (members: ts.NodeArray<ts.TypeElement>) => {
      const own = new Set<string>();
      for (const member of members) {
        if (
          (ts.isPropertySignature(member) || ts.isMethodSignature(member)) &&
          member.name &&
          ts.isIdentifier(member.name)
        ) {
          own.add(member.name.text);
        }
      }
      return own;
    };

    source.forEachChild((node) => {
      if (ts.isInterfaceDeclaration(node)) {
        const heritage = (node.heritageClauses ?? [])
          .flatMap((clause) => clause.types)
          .map((type) => type.expression)
          .filter(ts.isIdentifier)
          .map((identifier) => identifier.text);
        found.push({
          name: node.name.text,
          file,
          own: membersOf(node.members),
          heritage,
        });
        return;
      }

      if (!ts.isTypeAliasDeclaration(node)) return;

      const parts = ts.isIntersectionTypeNode(node.type)
        ? [...node.type.types]
        : [node.type];
      const own = new Set<string>();
      const heritage: string[] = [];
      for (const part of parts) {
        if (ts.isTypeLiteralNode(part)) {
          for (const member of membersOf(part.members)) own.add(member);
        } else if (
          ts.isTypeReferenceNode(part) &&
          ts.isIdentifier(part.typeName)
        ) {
          heritage.push(part.typeName.text);
        }
      }
      if (own.size > 0 || heritage.length > 0) {
        found.push({ name: node.name.text, file, own, heritage });
      }
    });
  }

  return found;
}

const declarations = readDeclarations();

const byName = new Map<string, Declaration[]>();
for (const declaration of declarations) {
  const siblings = byName.get(declaration.name);
  if (siblings) siblings.push(declaration);
  else byName.set(declaration.name, [declaration]);
}

function propertiesOf(
  declaration: Declaration,
  seen = new Set<string>(),
): Set<string> {
  const all = new Set(declaration.own);
  for (const base of declaration.heritage) {
    if (seen.has(base)) continue;
    seen.add(base);
    for (const inherited of byName.get(base) ?? []) {
      for (const property of propertiesOf(inherited, seen)) all.add(property);
    }
  }
  return all;
}

const snapshot: Record<string, string[]> = existsSync(SNAPSHOT)
  ? JSON.parse(readFileSync(SNAPSHOT, "utf8"))
  : {};

/**
 * Schema ids by their last segment, with generic arity and arguments dropped.
 * A generic schema is published under a name that carries its argument list
 * after a backtick, and everything from that backtick on is cut: the envelope
 * of a blog and the envelope of a topic both index under Envelope.
 */
const schemasByName = new Map<string, string[]>();
for (const id of Object.keys(snapshot)) {
  const name = id.split("`")[0].split(".").pop() ?? id;
  const siblings = schemasByName.get(name);
  if (siblings) siblings.push(id);
  else schemasByName.set(name, [id]);
}

/**
 * What a name may legitimately mirror: every schema published under it, each
 * on its own, plus the MIRRORS entry as one set. Several schemas can share a
 * last segment (two BlockUserRequest, two Tag), and agreeing with any one of
 * them is agreement.
 */
function candidatesFor(
  name: string,
): { schemas: string[]; served: Set<string> }[] {
  const candidates = (schemasByName.get(name) ?? []).map((id) => ({
    schemas: [id],
    served: new Set(snapshot[id]),
  }));

  const mirrored = MIRRORS[name];
  if (mirrored) {
    candidates.push({
      schemas: mirrored,
      served: new Set(mirrored.flatMap((id) => snapshot[id] ?? [])),
    });
  }

  return candidates;
}

const mirrors = declarations
  .map((declaration) => ({
    declaration,
    candidates: candidatesFor(declaration.name),
  }))
  .filter((pair) => pair.candidates.length > 0);

/** Fields the check lets a mirror declare without the server sending them. */
function exemptFor(name: string): Set<string> {
  return new Set([...(CLIENT_ONLY[name] ?? []), ...(UNSERVED[name] ?? [])]);
}

/** Entries of an exception list whose field no model declares any more. */
function undeclared(exceptions: Record<string, string[]>): string[] {
  return Object.entries(exceptions).flatMap(([name, properties]) => {
    const declared = new Set(
      (byName.get(name) ?? []).flatMap((declaration) => [
        ...propertiesOf(declaration),
      ]),
    );
    return properties
      .filter((property) => !declared.has(property))
      .map((property) => `${name}.${property}`);
  });
}

describe("API contract", () => {
  it("has a committed snapshot to check against", () => {
    // Not skipped when absent: a contract test that quietly passes with no
    // contract is worse than none. Run the integration suite to emit it.
    expect(
      existsSync(SNAPSHOT),
      `${SNAPSHOT} is missing. Run DM.Web.API.IntegrationTests OpenApiContractShould to emit it.`,
    ).toBe(true);
  });

  it("finds the mirrors to check", () => {
    // The pairing is derived, so a walk that finds nothing is a suite that
    // passes without checking anything — the exact silence this replaces.
    expect(
      declarations.length,
      `no object types were read under ${SOURCE_ROOT}`,
    ).toBeGreaterThan(200);
    expect(
      mirrors.length,
      "no client type matched a published schema; the snapshot or the walk is broken",
    ).toBeGreaterThan(100);
  });

  for (const [name, schemas] of Object.entries(MIRRORS)) {
    describe(`${name} (mapped by hand)`, () => {
      it("names schemas the API publishes", () => {
        expect(
          schemas.filter((id) => snapshot[id] === undefined),
          `${name} is mapped to schemas the contract does not have; the DTO was renamed or removed`,
        ).toEqual([]);
      });

      it("is declared in the client models", () => {
        expect(
          byName.has(name),
          `interface ${name} was not found under ${SOURCE_ROOT}`,
        ).toBe(true);
      });

      it("says something the name does not", () => {
        const served = new Set(schemas.flatMap((id) => snapshot[id] ?? []));
        const alreadyPaired = (schemasByName.get(name) ?? []).some((id) => {
          const byNameServed = new Set(snapshot[id]);
          return (
            byNameServed.size === served.size &&
            [...served].every((property) => byNameServed.has(property))
          );
        });

        // An entry the name rule would produce anyway is the beginning of the
        // second copy this mapping used to be.
        expect(
          alreadyPaired,
          `${name} is already paired by its name; the entry restates the rule`,
        ).toBe(false);
      });
    });
  }

  for (const { declaration, candidates } of mirrors) {
    const where = relative(SOURCE_ROOT, declaration.file).replace(/\\/g, "/");

    describe(`${declaration.name} (${where})`, () => {
      it("declares no property the API does not send", () => {
        const declared = propertiesOf(declaration);
        const exempt = exemptFor(declaration.name);
        const closest = candidates
          .map((candidate) => ({
            schemas: candidate.schemas,
            invented: [...declared].filter(
              (property) =>
                !candidate.served.has(property) && !exempt.has(property),
            ),
          }))
          .sort(
            (left, right) => left.invented.length - right.invented.length,
          )[0];

        expect(
          closest.invented,
          `${declaration.name} declares fields ${closest.schemas.join(", ")} does not publish: reading them yields undefined`,
        ).toEqual([]);
      });
    });
  }

  it("declares every property claimed as client-only", () => {
    // An exception for a field that no longer exists is an exception nobody
    // will notice has stopped applying.
    expect(
      undeclared(CLIENT_ONLY),
      "client-only fields that are no longer declared anywhere",
    ).toEqual([]);
  });

  it("declares every property recorded as unserved", () => {
    expect(
      undeclared(UNSERVED),
      "unserved fields that are no longer declared; drop the entry with the field",
    ).toEqual([]);
  });

  it("records as unserved only what the API still does not send", () => {
    const served = Object.entries(UNSERVED).flatMap(([name, properties]) => {
      const published = new Set(
        candidatesFor(name).flatMap((candidate) => [...candidate.served]),
      );
      return properties
        .filter((property) => published.has(property))
        .map((property) => `${name}.${property}`);
    });

    // The server caught up: the field is part of the contract now, and the
    // exception has to go or the next drift on it passes unnoticed.
    expect(
      served,
      "fields recorded as unserved that the API now publishes",
    ).toEqual([]);
  });
});
