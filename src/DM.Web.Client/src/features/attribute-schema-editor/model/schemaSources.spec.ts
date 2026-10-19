/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { attributeSchemaTemplateApi } from "@/entities/attribute-schema-template";
import { gameApi, gameCatalogApi, GameParticipation } from "@/entities/game";
import { useToast } from "@/shared/lib/composables/useToast";
import { useSchemaSources, type SchemaSource } from "./schemaSources";

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
  vi.spyOn(gameCatalogApi, "getParticipatingGames").mockResolvedValue({
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

const gameEntry = (sources: SchemaSource[]) =>
  sources.find((s) => s.value === "game:led-game")!;

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
