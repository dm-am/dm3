/**
 * @vitest-environment node
 */

/**
 * The hand-written mirrors of C# DTOs against the published schemas
 * (artifacts/openapi-contract.json, by OpenApiContractShould), by property
 * name only. A client type whose name is the last segment of a schema id
 * mirrors it, so a mirror written today is checked today; a view model may
 * not borrow a DTO's name.
 */
import { describe, it, expect } from "vitest";
import { readFileSync, existsSync } from "fs";
import { join, relative, resolve } from "path";
import ts from "typescript";
import {
  CLIENT_SRC as SOURCE_ROOT,
  collect,
} from "@/shared/lib/testing/sourceTree";

// src -> DM.Web.Client -> src -> repository root
const REPO_ROOT = resolve(SOURCE_ROOT, "..", "..", "..");
const SNAPSHOT = join(REPO_ROOT, "artifacts", "openapi-contract.json");

/**
 * Mirrors whose name does not name their schema: a rename (one interface for
 * list and detail DTOs) or a union (one interface for several shapes). An
 * entry the name already implies fails.
 */
const MIRRORS: Record<string, string[]> = {
  Game: ["DM.Web.API.Features.Game.Games.GameDetails"],
  Character: ["DM.Web.API.Features.Game.Characters.CharacterDetails"],
  NotepadEntry: ["DM.Web.API.Features.Personal.Notepads.NotepadEntryResponse"],

  // The account's view of its username change, named after its request; the
  // moderation type of that name matches its schema by name.
  UsernameChangeRequest: [
    "DM.Web.API.Features.Account.Credentials.UsernameChangeResponse",
  ],

  // A row of the list of notifications. The client named it after its reader,
  // the server after the one DTO the list and the hub both send.
  UserNotification: ["DM.Web.API.Features.Personal.Notifications.Notification"],

  // One interface over three server shapes, every extra field optional and
  // commented as such. Held to the union of the three: a field that none of
  // them sends is still a field that reads undefined.
  User: [
    "DM.Web.API.Features.Community.Users.User",
    "DM.Web.API.Features.Community.Users.UserProfile",
    "DM.Web.API.Features.Personal.Profiles.PersonalProfile",
  ],
};

/** Properties a client model owns: optimistic state a store writes, named on purpose. */
const CLIENT_ONLY: Record<string, string[]> = {
  // Set after a successful delete so the entry becomes a placeholder instead
  // of vanishing under the reader. No endpoint returns a removed comment, and
  // a reload drops it entirely.
  Comment: ["isRemoved"],
};

/**
 * Fields a mirror declares that no schema sends, each undefined at runtime;
 * recorded, since removing one is a product decision. Checked both ways: an
 * entry for a field gone from the model or now sent fails.
 */
const UNSERVED: Record<string, string[]> = {
  // `settings` is published by no user schema (preferences have their own
  // endpoint); the session fills it in on the client.
  User: ["settings"],

  // Taking part in the rating is a visibility setting of the profile
  // (users/me/profile, `visibility.showRating`), and the server answers one
  // who left it with a null rating, not a flag.
  Rating: ["isEnabled", "totalRating"],

  // The wire sends the old name and the date. The profile keys its list on
  // `entry.id`, which is undefined for every entry.
  UsernameHistoryEntry: ["id", "newUsername", "approvedByUsername"],
};

/** A named object type declared somewhere in the client. */
type Declaration = {
  name: string;
  file: string;
  own: Set<string>;
  heritage: string[];
};

/** Every TypeScript source under src/, specs and ambient declarations aside. */
const collectFiles = (dir: string): string[] =>
  collect(
    dir,
    (name) =>
      name.endsWith(".ts") &&
      !name.endsWith(".spec.ts") &&
      !name.endsWith(".d.ts"),
  );

/** Every declaration site: two slices can hold different types under one name. */
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
 * Schema ids by their last segment without generic arity: the envelope of a
 * blog and of a topic both index under Envelope.
 */
const schemasByName = new Map<string, string[]>();
for (const id of Object.keys(snapshot)) {
  const name = id.split("`")[0].split(".").pop() ?? id;
  const siblings = schemasByName.get(name);
  if (siblings) siblings.push(id);
  else schemasByName.set(name, [id]);
}

/** What a name may mirror: each schema under it, or the MIRRORS entry as one set. */
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
