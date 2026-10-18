/**
 * What the "Схема атрибутов" list over the attribute table offers.
 *
 * One list, three kinds of entry (doc 4.2.2.10): the site's templates with the
 * default one first and picked, then the sheets of the games the reader leads,
 * and the empty sheet last. The editor itself knows none of that: it is handed
 * the entries and asks the picked one for its specifications when "Скопировать"
 * is pressed.
 *
 * The reading is deferred per entry because a game's sheet arrives with the
 * game's details, which is a request per game; a list of them at open time
 * would read every game the reader leads to fill a control they may not touch.
 */
import { ref, type Ref } from "vue";
import {
  attributeSchemaTemplateApi,
  type AttributeSchemaTemplate,
} from "@/entities/attribute-schema-template";
import {
  gameApi,
  GameParticipation,
  type AttributeSpecification,
  type Game,
  type GameRef,
} from "@/entities/game";
import { unwrapResource } from "@/shared/api";
import {
  describeFailure,
  describeRateLimit,
  refusalHere,
} from "@/shared/lib/errors";

/** One entry of the list. */
export interface SchemaSource {
  /** The option value; the list is a native select, so it is a string. */
  value: string;
  label: string;
  /**
   * The specifications to copy, read when the reader asks for them. Null means
   * the read failed and `refusal` says why, where it says anything.
   */
  read: () => Promise<AttributeSpecification[] | null>;
}

/** Option value of the entry that copies nothing. */
const EMPTY_SOURCE_VALUE = "empty";

const TEMPLATES_FAILED = "Не удалось загрузить шаблоны схем";
const GAME_FAILED = "Не удалось загрузить игру";

/**
 * The statuses the interceptor announces even for a request that owns its
 * refusal: a dead connection and an expired session are about the reader and
 * their network, not about the request (UI_STANDARDS, "Всплывающие
 * сообщения").
 */
const SAID_BY_INTERCEPTOR = [0, 401];

/**
 * The catalogue read answers for itself, in the line under the list.
 *
 * A list short of its templates says nothing about why on its own, and a
 * plate in the corner over a form that still works explains nothing about the
 * list. So the request takes the refusal over and the line carries it, the
 * wait included: the title of a 429 names no number.
 */
const OWNS_REFUSAL = { ownsRefusal: true, ownsRateLimit: true } as const;

/**
 * The entry that empties the editor. Last in the list, and the only way to a
 * game with no attributes at all: an empty sheet is a choice the lead makes,
 * never a state the site falls into.
 */
const emptySource: SchemaSource = {
  value: EMPTY_SOURCE_VALUE,
  label: "Пустая схема",
  read: async () => [],
};

const templateSource = (template: AttributeSchemaTemplate): SchemaSource => ({
  value: `template:${template.id}`,
  label: template.title,
  read: async () => template.specifications,
});

/**
 * A game of the reader's own, named as such: a template and a game may carry
 * the same name, and the two do not behave alike behind the button, so a bare
 * title left the reader picking between two identical lines.
 *
 * @param refusal Where a refused read of the game says why.
 */
const gameSource = (game: GameRef, refusal: Ref<string>): SchemaSource => ({
  value: `game:${game.id}`,
  label: `Из моей игры: ${game.title}`,
  read: async () => {
    // The sheet of a game is read with its details and nowhere else: there is
    // one path to it, and a second endpoint would be a second answer to keep
    // in step with the first.
    const { data, error } = await gameApi.getGame(game.publicId ?? game.id);
    if (error) {
      // The method has no seam for `ownsRefusal`, so the line takes the
      // plate the interceptor held for the refusal down instead, and prints
      // every refusal in the server's words.
      refusal.value = refusalHere(error, GAME_FAILED) ?? "";
      return null;
    }
    return unwrapResource<Game>(data)?.schema?.specifications ?? [];
  },
});

/**
 * Loads the list and the sheet the creation form opens on.
 *
 * Both come out of the same read of the catalogue, so a host that pre-fills
 * itself from the default template makes no request of its own for it.
 */
export function useSchemaSources() {
  const sources = ref<SchemaSource[]>([]);
  /**
   * Specifications of the default template, or null when the site has none.
   * A site with no default is the state before the first seed, and a form that
   * opens on it opens empty.
   */
  const defaultSpecifications = ref<AttributeSpecification[] | null>(null);
  /**
   * Why the list is short of its templates, or why the last copy brought
   * nothing: the host hands it to the editor, which prints it under the list.
   *
   * The reader pressed "Скопировать" beside that list, or is looking at it
   * for the templates that are not there; a plate in the corner is the one
   * place they are not looking (UI_STANDARDS, "Всплывающие сообщения"). One
   * line for both, answering the last attempt: cleared by the next read of
   * the catalogue and by the next copy, whichever entry it copies.
   */
  const refusal = ref("");

  /** The entry with its read clearing the line first: a copy is the retry. */
  const clearing = (source: SchemaSource): SchemaSource => ({
    ...source,
    read: async () => {
      refusal.value = "";
      return source.read();
    },
  });

  async function load(): Promise<void> {
    refusal.value = "";
    const [catalogue, games] = await Promise.all([
      readTemplates(),
      readAuthorityGames(),
    ]);

    sources.value = [
      ...catalogue.templates.map(templateSource),
      ...games.map((game) => gameSource(game, refusal)),
      emptySource,
    ].map(clearing);
    defaultSpecifications.value =
      catalogue.templates.find((t) => t.isDefault)?.specifications ?? null;
    refusal.value = catalogue.refusal;
  }

  return { sources, defaultSpecifications, refusal, load };
}

/**
 * The catalogue, in the order the server sends it: the default template first,
 * the rest by name. A refusal leaves the list without templates rather than
 * without the editor, so the lead still writes a sheet by hand, and says why
 * in `refusal` where the interceptor has not said it already.
 */
async function readTemplates(): Promise<{
  templates: AttributeSchemaTemplate[];
  refusal: string;
}> {
  const { data, error } =
    await attributeSchemaTemplateApi.getTemplates(OWNS_REFUSAL);
  if (error) {
    if (SAID_BY_INTERCEPTOR.includes(error.status)) {
      return { templates: [], refusal: "" };
    }
    // A wait is said in the shared wording, the one the interceptor would
    // have raised: it is the only one that names the seconds the server asked
    // for.
    return {
      templates: [],
      refusal:
        error.status === 429
          ? describeRateLimit(error.retryAfterSeconds)
          : describeFailure(error, TEMPLATES_FAILED),
    };
  }
  return { templates: data?.resources ?? [], refusal: "" };
}

/** The games the reader leads: master or assistant, which is one flag. */
async function readAuthorityGames(): Promise<GameRef[]> {
  const { data, error } = await gameApi.getParticipatingGames();
  // A guest has no games and the request is refused for him; the interceptor
  // owns that sentence, and the creation form is not the place to repeat it.
  if (error) return [];
  return (data?.resources ?? []).filter((game) =>
    (game.participation ?? []).includes(GameParticipation.Authority),
  );
}
