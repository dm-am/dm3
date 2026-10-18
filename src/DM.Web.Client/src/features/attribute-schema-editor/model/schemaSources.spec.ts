/**
 * @vitest-environment jsdom
 */

/**
 * The "Схема атрибутов" list, and what it says when a read behind it fails.
 *
 * Two reads can fail: the catalogue of templates when the list is built, and
 * the details of a game when its entry is copied. Both used to raise a plate
 * in the corner - over a form that still works, about a control the reader is
 * looking at - and now both answer in one line under that control, which the
 * host hands to the editor (AttributeSchemaEditor.spec pins the printing).
 *
 * The catalogue read owns its refusal, because the api has the seam: the line
 * carries a 403 and a 5xx in the server's words and says a wait in the shared
 * wording, with the seconds. The game read has no seam, so it keeps quiet on
 * the statuses the interceptor announces. Either way the line answers the last
 * attempt: the next read of the catalogue and the next copy clear it.
 *
 * No plate is raised in any test here: the interceptor lives in the axios
 * instance and the api is doubled above it, so what is measured is that the
 * list itself says nothing in the corner.
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { attributeSchemaTemplateApi } from "@/entities/attribute-schema-template";
import { gameApi, GameParticipation } from "@/entities/game";
import { useToast } from "@/shared/lib/composables/useToast";
import { useSchemaSources, type SchemaSource } from "./schemaSources";

/** A problem document with the fields the list reads. */
const refused = (status: number, title = "", retryAfterSeconds?: number) =>
  ({
    data: null,
    error: { type: "", title, status, traceId: "t", retryAfterSeconds },
  }) as never;

const catalogue = () =>
  ({
    data: {
      resources: [
        {
          id: "default",
          title: "Базовая",
          isDefault: true,
          specifications: [],
        },
      ],
      paging: null,
    },
    error: null,
  }) as never;

const serveTemplates = () =>
  vi
    .spyOn(attributeSchemaTemplateApi, "getTemplates")
    .mockResolvedValue(catalogue());

const refuseTemplates = (...args: Parameters<typeof refused>) =>
  vi
    .spyOn(attributeSchemaTemplateApi, "getTemplates")
    .mockResolvedValue(refused(...args));

const serveGames = () =>
  vi.spyOn(gameApi, "getParticipatingGames").mockResolvedValue({
    data: {
      resources: [
        {
          id: "led-game",
          publicId: "led",
          title: "Моя игра",
          participation: [GameParticipation.Authority],
        },
      ],
      paging: null,
    },
    error: null,
  } as never);

const refuseGame = (...args: Parameters<typeof refused>) =>
  vi.spyOn(gameApi, "getGame").mockResolvedValue(refused(...args));

/** The list's entry for the game the reader leads. */
const gameEntry = (sources: SchemaSource[]) =>
  sources.find((s) => s.value === "game:led-game")!;

/** Every plate on screen, by its sentence. */
const plates = () => useToast().toasts.value.map((t) => t.message);

afterEach(() => {
  vi.restoreAllMocks();
  const { toasts, dismiss } = useToast();
  [...toasts.value].forEach((t) => dismiss(t.id));
});

describe("the catalogue read", () => {
  it("takes its refusal over from the interceptor, the wait included", async () => {
    const getTemplates = serveTemplates();
    serveGames();

    await useSchemaSources().load();

    expect(getTemplates).toHaveBeenCalledWith({
      ownsRefusal: true,
      ownsRateLimit: true,
    });
  });

  it("says why the list is short of templates, in the server's words, and offers the rest", async () => {
    refuseTemplates(503, "Ошибка сервера");
    serveGames();
    const { sources, defaultSpecifications, refusal, load } =
      useSchemaSources();

    await load();

    expect(refusal.value).toBe("Ошибка сервера");
    expect(sources.value.map((s) => s.label)).toEqual([
      "Из моей игры: Моя игра",
      "Пустая схема",
    ]);
    expect(defaultSpecifications.value).toBeNull();
    expect(plates()).toEqual([]);
  });

  it("falls back to its own sentence when the server named nothing", async () => {
    refuseTemplates(404);
    serveGames();
    const { refusal, load } = useSchemaSources();

    await load();

    expect(refusal.value).toBe("Не удалось загрузить шаблоны схем");
  });

  it("says a wait with the seconds the server asked for", async () => {
    refuseTemplates(429, "Too Many Requests", 30);
    serveGames();
    const { refusal, load } = useSchemaSources();

    await load();

    expect(refusal.value).toBe(
      "Слишком много попыток, попробуйте через 30 секунд",
    );
  });

  // A dead connection and an expired session are announced whatever the
  // request owns: they are about the reader, not about the catalogue.
  it.each([0, 401])(
    "stays quiet about a status %i, which the interceptor announces anyway",
    async (status) => {
      refuseTemplates(status, "Ошибка сервера");
      serveGames();
      const { refusal, load } = useSchemaSources();

      await load();

      expect(refusal.value).toBe("");
      expect(plates()).toEqual([]);
    },
  );

  it("clears the line when the catalogue is read again", async () => {
    vi.spyOn(attributeSchemaTemplateApi, "getTemplates")
      .mockResolvedValueOnce(refused(404))
      .mockResolvedValueOnce(catalogue());
    serveGames();
    const { refusal, load } = useSchemaSources();

    await load();
    expect(refusal.value).toBe("Не удалось загрузить шаблоны схем");

    await load();

    expect(refusal.value).toBe("");
  });
});

describe("copying a game's sheet", () => {
  it("says why the copy brought nothing, in the server's words, and copies nothing", async () => {
    serveTemplates();
    serveGames();
    refuseGame(404, "Игра не найдена");
    const { sources, refusal, load } = useSchemaSources();
    await load();

    expect(await gameEntry(sources.value).read()).toBeNull();

    expect(refusal.value).toBe("Игра не найдена");
    expect(plates()).toEqual([]);
  });

  it("falls back to its own sentence when the server named nothing", async () => {
    serveTemplates();
    serveGames();
    refuseGame(404);
    const { sources, refusal, load } = useSchemaSources();
    await load();

    await gameEntry(sources.value).read();

    expect(refusal.value).toBe("Не удалось загрузить игру");
  });

  // The game read has no seam for `ownsRefusal`: the line takes the plate
  // the interceptor held down instead, and says the refusal itself.
  it.each([
    [403, "Ошибка сервера"],
    [429, "Слишком много попыток, попробуйте позже"],
    [500, "Ошибка сервера"],
    [503, "Ошибка сервера"],
  ])("says a read refused with status %i in the line", async (status, line) => {
    serveTemplates();
    serveGames();
    refuseGame(status, "Ошибка сервера");
    const { sources, refusal, load } = useSchemaSources();
    await load();

    expect(await gameEntry(sources.value).read()).toBeNull();

    expect(refusal.value).toBe(line);
    expect(plates()).toEqual([]);
  });

  // An expired session and a dead connection keep the plate.
  it.each([0, 401])(
    "leaves a read refused with status %i to the interceptor",
    async (status) => {
      serveTemplates();
      serveGames();
      refuseGame(status, "Ошибка сервера");
      const { sources, refusal, load } = useSchemaSources();
      await load();

      expect(await gameEntry(sources.value).read()).toBeNull();

      expect(refusal.value).toBe("");
      expect(plates()).toEqual([]);
    },
  );

  it("clears the line on the next copy, whichever entry it copies", async () => {
    serveTemplates();
    serveGames();
    refuseGame(404, "Игра не найдена");
    const { sources, refusal, load } = useSchemaSources();
    await load();
    await gameEntry(sources.value).read();
    expect(refusal.value).toBe("Игра не найдена");

    // The empty sheet, which reaches for nothing and cannot fail.
    expect(await sources.value.at(-1)!.read()).toEqual([]);

    expect(refusal.value).toBe("");
  });
});
