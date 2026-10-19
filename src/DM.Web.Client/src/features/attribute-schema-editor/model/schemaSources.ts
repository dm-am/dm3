// Each entry reads its sheet on "Скопировать": a game's sheet is a request per game.
import { ref, type Ref } from "vue";
import {
  attributeSchemaTemplateApi,
  type AttributeSchemaTemplate,
} from "@/entities/attribute-schema-template";
import {
  gameApi,
  gameCatalogApi,
  GameParticipation,
  type AttributeSpecification,
  type Game,
  type GameRef,
} from "@/entities/game";
import { unwrapResource } from "@/shared/api";
import { describeFailureOrWait, refusalHere } from "@/shared/lib/errors";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

export interface SchemaSource {
  /** The list is a native select, so the value is a string. */
  value: string;
  label: string;
  /** Null means the read failed and `refusal` says why. */
  read: () => Promise<AttributeSpecification[] | null>;
}

const EMPTY_SOURCE_VALUE = "empty";

const TEMPLATES_FAILED = "Не удалось загрузить шаблоны схем";
const GAME_FAILED = "Не удалось загрузить игру";

// A dead connection and an expired session are announced by the interceptor anyway.
const SAID_BY_INTERCEPTOR = [0, 401];

// The only way to an empty sheet: a choice the lead makes, never a state the site falls into.
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

// Named as the reader's game: a template and a game may share a title.
const gameSource = (game: GameRef, refusal: Ref<string>): SchemaSource => ({
  value: `game:${game.id}`,
  label: `Из моей игры: ${game.title}`,
  read: async () => {
    const { data, error } = await gameApi.getGame(game.publicId ?? game.id);
    if (error) {
      // The method has no seam for `ownsRefusal`, so the line takes the plate down.
      refusal.value = refusalHere(error, GAME_FAILED) ?? "";
      return null;
    }
    return unwrapResource<Game>(data)?.schema?.specifications ?? [];
  },
});

// The default template comes from the same catalogue read, so the host asks nothing more.
export function useSchemaSources() {
  const sources = ref<SchemaSource[]>([]);
  /** Null before the first seed, when the site has no default template. */
  const defaultSpecifications = ref<AttributeSpecification[] | null>(null);
  /** Under the list, answering the last attempt: cleared by the next read and the next copy. */
  const refusal = ref("");

  // A copy is the retry, so it clears the line first.
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

// A refusal leaves the list without templates, not the lead without an editor.
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
    // The shared wording of a wait: the only one that names the seconds.
    return {
      templates: [],
      refusal: describeFailureOrWait(error, TEMPLATES_FAILED),
    };
  }
  return { templates: data?.resources ?? [], refusal: "" };
}

// The games the reader leads: master or assistant, one flag.
async function readAuthorityGames(): Promise<GameRef[]> {
  const { data, error } = await gameCatalogApi.getParticipatingGames();
  // A guest is refused here, and the interceptor says so.
  if (error) return [];
  return (data?.resources ?? []).filter((game) =>
    (game.participation ?? []).includes(GameParticipation.Authority),
  );
}
