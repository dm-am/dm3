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
      // Without the deletion clause: that is what 410 below says, and while
      // both said it the two pages answered the same question. This one is the
      // wrong address, nothing more.
      "По этому адресу ничего нет. Проверьте ссылку, возможно, в ней ошибка.",
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
  // The rate limit of the API: the page is there, the reader only asked for
  // it too often. Read as "не найдено", it sent him to check a link that was
  // right all along.
  429: {
    title: "Слишком много запросов",
    description: "Подождите немного и обновите страницу.",
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
 * The page a failed load's HTTP status deserves.
 *
 * One mapping, because three had already diverged: the forum board page turned
 * every status but 404 into "ошибка сервера", and a missing board was answered
 * with 410 back then, so that is what a mistyped alias actually showed; the
 * topic page next to it mapped the same 410 to "Страница удалена"; the profile
 * page had a third spelling. A game and a blog had no mapping at all and drew
 * one paragraph for everything, so a reader refused access could not tell it
 * from a network failure.
 *
 * The API now spends 410 on one meaning: a resource that existed and was
 * deleted (API_DESIGN.md). A board, a game or a profile that is not there
 * answers 404. The topic endpoint is the one that still separates the two
 * (TopicService answers Gone for a deleted topic and 404 for one that never
 * existed), so `goneMeansRemoved` stays as the opt-in of the page that reads
 * 410 literally. The default keeps 410 as "не найдено" so a stray one from any
 * other endpoint never reports a deletion that did not happen.
 *
 * Every other status with a page of its own gets that page: 429 is the rate
 * limit and not a wrong address, 401 asks the reader to sign in. A status
 * without one gets the general page, never "не найдено": collapsing every 4xx
 * there sent a reader stopped by the rate limit off to check a link that was
 * right. A missing status or a 5xx is a fault on our side.
 */
export function errorCodeForStatus(
  status: number | undefined,
  options: { goneMeansRemoved?: boolean } = {},
): number {
  if (!status || status >= 500) return 500;
  if (status === 410) return options.goneMeansRemoved ? 410 : 404;
  return status;
}
