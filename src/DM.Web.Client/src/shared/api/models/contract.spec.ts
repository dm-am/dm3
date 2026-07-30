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
 * The mapping is explicit on purpose. Schema ids are fully qualified .NET type
 * names, so matching them to TypeScript by convention would either miss pairs
 * or invent them.
 */
import { describe, it, expect } from "vitest";
import { readFileSync, existsSync, readdirSync, statSync } from "fs";
import { dirname, join, resolve } from "path";
import { fileURLToPath } from "url";
import ts from "typescript";

const HERE = dirname(fileURLToPath(import.meta.url));
// models -> api -> shared -> src -> DM.Web.Client
const CLIENT_ROOT = resolve(HERE, "..", "..", "..", "..");
const REPO_ROOT = resolve(CLIENT_ROOT, "..", "..");
const SNAPSHOT = join(REPO_ROOT, "artifacts", "openapi-contract.json");

/** Schema id in the OpenAPI document -> interface in the client models. */
const MAPPING: Record<string, string> = {
  // The client keeps one interface where the API publishes a list DTO and a
  // detail DTO, and it mirrors the richer one.
  "DM.Web.API.Features.Game.Games.GameDetails": "Game",
  "DM.Web.API.Features.Game.Games.GameRef": "GameRef",
  "DM.Web.API.Features.Game.Posts.Post": "Post",
  "DM.Web.API.Features.Game.Characters.CharacterDetails": "Character",
  "DM.Web.API.Features.Blog.Blogs.Blog": "Blog",
  "DM.Web.API.Features.Blog.Blogs.BlogRef": "BlogRef",
  "DM.Web.API.Features.Blog.Publications.Publication": "Publication",
  "DM.Web.API.Features.Forum.Topics.Topic": "Topic",
  "DM.Web.API.Shared.Dto.UserRef": "UserRef",
  "DM.Web.API.Shared.Dto.Upload": "Upload",
  "DM.Web.API.Features.Messaging.Messages.Message": "Message",
  "DM.Web.API.Features.Personal.Notepads.NotepadEntryResponse": "NotepadEntry",
  "DM.Web.API.Shared.Dto.Comment": "Comment",

  // Deliberately absent: User. The client interface is one union of three
  // server shapes (User, UserProfile and the community-list projection) with
  // every extra field optional and commented as such. There is no single
  // schema to hold it to.
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
 * Where the mirrored interfaces live. Entity slices declare their own copies,
 * so both roots are read and the first declaration of a name wins — the same
 * order a consumer would resolve it in.
 */
const MODEL_GLOBS = [
  "src/shared/api/models",
  "src/entities/game/model",
  "src/entities/blog/model",
  "src/entities/forum/model",
  "src/entities/message/model",
  "src/entities/user/model",
];

function collectFiles(dir: string, out: string[] = []): string[] {
  if (!existsSync(dir)) return out;
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) collectFiles(full, out);
    else if (full.endsWith(".ts") && !full.endsWith(".spec.ts")) out.push(full);
  }
  return out;
}

/** interface name -> its own property names plus the names it extends */
function readInterfaces(): Map<
  string,
  { own: Set<string>; heritage: string[] }
> {
  const found = new Map<string, { own: Set<string>; heritage: string[] }>();

  for (const root of MODEL_GLOBS) {
    for (const file of collectFiles(join(CLIENT_ROOT, root))) {
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
          const name = node.name.text;
          if (found.has(name)) return;
          const heritage = (node.heritageClauses ?? [])
            .flatMap((clause) => clause.types)
            .map((type) => type.expression)
            .filter(ts.isIdentifier)
            .map((identifier) => identifier.text);
          found.set(name, { own: membersOf(node.members), heritage });
          return;
        }

        if (!ts.isTypeAliasDeclaration(node)) return;
        const name = node.name.text;
        if (found.has(name)) return;

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
        if (own.size > 0 || heritage.length > 0)
          found.set(name, { own, heritage });
      });
    }
  }

  return found;
}

function propertiesOf(
  name: string,
  interfaces: ReturnType<typeof readInterfaces>,
  seen = new Set<string>(),
): Set<string> {
  if (seen.has(name)) return new Set();
  seen.add(name);

  const declaration = interfaces.get(name);
  if (!declaration) return new Set();

  const all = new Set(declaration.own);
  for (const base of declaration.heritage) {
    for (const inherited of propertiesOf(base, interfaces, seen))
      all.add(inherited);
  }
  return all;
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

  const snapshot: Record<string, string[]> = existsSync(SNAPSHOT)
    ? JSON.parse(readFileSync(SNAPSHOT, "utf8"))
    : {};
  const interfaces = readInterfaces();

  for (const [schemaId, interfaceName] of Object.entries(MAPPING)) {
    describe(`${interfaceName}`, () => {
      it("is mapped to a schema the API publishes", () => {
        expect(
          snapshot[schemaId],
          `${schemaId} is not in the contract snapshot; the DTO was renamed or removed`,
        ).toBeDefined();
      });

      it("is declared in the client models", () => {
        expect(
          interfaces.has(interfaceName),
          `interface ${interfaceName} was not found under ${MODEL_GLOBS.join(", ")}`,
        ).toBe(true);
      });

      it("declares no property the API does not send", () => {
        const served = new Set(snapshot[schemaId] ?? []);
        const local = new Set(CLIENT_ONLY[interfaceName] ?? []);
        const declared = propertiesOf(interfaceName, interfaces);
        const invented = [...declared].filter(
          (p) => !served.has(p) && !local.has(p),
        );

        expect(
          invented,
          `${interfaceName} declares fields the API does not publish: reading them yields undefined`,
        ).toEqual([]);
      });

      it("declares every property it claims as client-only", () => {
        const declared = propertiesOf(interfaceName, interfaces);
        const stale = (CLIENT_ONLY[interfaceName] ?? []).filter(
          (p) => !declared.has(p),
        );

        // An exception for a field that no longer exists is an exception
        // nobody will notice has stopped applying.
        expect(
          stale,
          `${interfaceName} lists client-only fields it no longer declares`,
        ).toEqual([]);
      });
    });
  }
});
