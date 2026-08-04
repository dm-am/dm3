/**
 * Error page config resolution.
 * @module shared/ui/ErrorPage/errorConfig
 *
 * Pure lookup extracted from ErrorPage.vue so pages can resolve title/
 * description/illustration for a given HTTP code without mounting the
 * component (e.g. for document title composables).
 */
import img400 from "@/assets/images/errors/400.png";
import img401 from "@/assets/images/errors/401.png";
import img403 from "@/assets/images/errors/403.png";
import img500 from "@/assets/images/errors/500.png";
import imgGeneral from "@/assets/images/errors/general-error.png";

interface ErrorImage {
  src: string;
  width: number;
  height: number;
}

export interface ErrorConfig {
  title: string;
  description: string;
  showBack?: boolean;
  image: ErrorImage;
}

// TODO(round-2 art): dedicated illustrations for 404 / 409 / 410 are missing.
// They currently fall back to general-error.png. Add 404 (empty chest),
// 409 (fighting goblins), 410 (ashes) per docs/plans/ERROR_PAGES_AND_LORE.md
// and extend this map. 404 is the most visited error page (catch-all route).
const generalImage: ErrorImage = { src: imgGeneral, width: 400, height: 265 };

const errorDefaults: Record<number, ErrorConfig> = {
  400: {
    title: "Неверный запрос",
    description:
      "Сервер не понял запрос. Проверьте введенные данные и попробуйте снова.",
    image: { src: img400, width: 800, height: 800 },
  },
  401: {
    title: "Требуется авторизация",
    // No description here: the 401 template branch renders the sentence
    // inline with router-links to login/register (see ErrorPage.vue), so a
    // plain string would either duplicate or be unused. A custom
    // `description` prop still overrides the inline branch when passed.
    description: "",
    showBack: false,
    image: { src: img401, width: 800, height: 800 },
  },
  403: {
    title: "Доступ запрещен",
    description:
      "У вас недостаточно прав для просмотра этой страницы. Возможно, доступ ограничен автором или администрацией.",
    image: { src: img403, width: 800, height: 800 },
  },
  404: {
    title: "Страница не найдена",
    description:
      "Такой страницы не существует. Возможно, она была перемещена или удалена.",
    image: generalImage,
  },
  409: {
    title: "Конфликт данных",
    description:
      "При обработке запроса произошел конфликт. Попробуйте повторить действие.",
    image: generalImage,
  },
  410: {
    title: "Страница удалена",
    description:
      "Этой страницы больше не существует. Возможно, она была удалена автором или модератором.",
    image: generalImage,
  },
  500: {
    title: "Произошла ошибка сервера",
    description: "Что-то сломалось на нашей стороне. Попробуйте немного позже.",
    image: { src: img500, width: 800, height: 800 },
  },
};

const fallbackConfig: ErrorConfig = {
  title: "Неизвестная ошибка",
  description:
    "Что-то пошло не так. Попробуйте вернуться и повторить действие.",
  image: generalImage,
};

/** Resolves title/description/illustration for an HTTP error code. */
export function getErrorConfig(code: number): ErrorConfig {
  return errorDefaults[code] ?? fallbackConfig;
}

/**
 * The error page an entity shell owes a failed fetch.
 *
 * A missing page, a page the viewer may not open, a page that was deleted and a
 * server that fell over are four different things to say, and the status is the
 * only place the difference lives. Written out once because three shells — the
 * topic, the game, the blog — each need the same four answers; the game and the
 * blog had none of them and said "Не удалось загрузить" to all four, which
 * reads as "try again" to someone following a link to something deleted.
 *
 * A 4xx this map does not name is a bad address, so it lands on 404; no status
 * at all means the request never left, which is 500's sentence.
 */
export function toErrorPageCode(status: number | undefined): number {
  if (status === 403) return 403;
  if (status === 410) return 410;
  if (status === 404) return 404;
  if (!status || status >= 500) return 500;
  return 404;
}

/**
 * The page a failed load's HTTP status deserves.
 *
 * One mapping, because three had already diverged: the forum board page turned
 * every status but 404 into "ошибка сервера", and since the API answers a
 * missing board with 410 that is what a mistyped alias actually showed; the
 * topic page next to it mapped the same 410 to "Страница удалена"; the profile
 * page had a third spelling. A game and a blog had no mapping at all and drew
 * one paragraph for everything, so a reader refused access could not tell it
 * from a network failure.
 *
 * 410 is the one status that means two things here, so the caller says which.
 * The default is "не найдено", because the API spends Gone on "no such board /
 * user"; `goneMeansRemoved` is the opt-in of the pages that read it literally.
 * The topic page has that distinction from the server (TopicService answers
 * Gone for a deleted topic and 404 for one that never existed), so it is the
 * page that asks. The game and blog shells do not ask: GameService answers Gone
 * for every id that addresses nothing this reader may see, so a mistyped address
 * would be reported to them as somebody having deleted the game.
 *
 * Everything else collapses on purpose: a status with no page of its own reads
 * as "not found", and a missing status or a 5xx as a fault on our side.
 */
export function errorCodeForStatus(
  status: number | undefined,
  options: { goneMeansRemoved?: boolean } = {},
): number {
  if (!status || status >= 500) return 500;
  if (status === 403) return 403;
  if (status === 410) return options.goneMeansRemoved ? 410 : 404;
  return 404;
}

/**
 * Symbolic (non-numeric) error codes surfaced at /error/:code and
 * /error?code= — chiefly OAuth-style authorization failures (doc 4.2.3.1.6).
 * Each borrows the illustration/behavior of a matching HTTP status via `code`
 * and carries a human-readable title/description.
 */
export interface SymbolicErrorConfig {
  /** HTTP status whose illustration/behavior this symbolic code reuses. */
  code: number;
  title: string;
  description: string;
}

const symbolicErrors: Record<string, SymbolicErrorConfig> = {
  expired_token: {
    code: 410,
    title: "Ссылка устарела",
    description:
      "Срок действия ссылки истек. Запросите новую и попробуйте снова.",
  },
  invalid_redirect_uri: {
    code: 400,
    title: "Неверный адрес перенаправления",
    description:
      "Адрес перенаправления не распознан. Вернитесь назад и попробуйте снова.",
  },
  access_denied: {
    code: 403,
    title: "Доступ не предоставлен",
    description: "Запрос был отклонен или у вас нет прав на это действие.",
  },
};

/** Resolves a symbolic error code, or null when it is not a known symbol. */
export function getSymbolicError(code: string): SymbolicErrorConfig | null {
  return symbolicErrors[code] ?? null;
}
