// A pure lookup, so a page resolves an error's title without mounting the component.
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

const generalImage: ErrorImage = { src: imgGeneral, width: 400, height: 265 };

/** The illustrations of their own are square. */
const square = (src: string): ErrorImage => ({ src, width: 800, height: 800 });

const errorDefaults: Record<number, ErrorConfig> = {
  400: {
    title: "Неверный запрос",
    description:
      "Сервер не понял запрос. Проверьте введенные данные и попробуйте снова.",
    image: square(img400),
  },
  401: {
    title: "Требуется авторизация",
    // The 401 sentence is the template's, with its links.
    description: "",
    showBack: false,
    image: square(img401),
  },
  403: {
    title: "Доступ запрещен",
    description:
      "У вас недостаточно прав для просмотра этой страницы. Возможно, доступ ограничен автором или администрацией.",
    image: square(img403),
  },
  404: {
    title: "Страница не найдена",
    description:
      // Not the deletion clause: that is 410's.
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
  // The rate limit: the page is there, the reader asked for it too often.
  429: {
    title: "Слишком много запросов",
    description: "Подождите немного и обновите страницу.",
    image: generalImage,
  },
  500: {
    title: "Произошла ошибка сервера",
    description: "Что-то сломалось на нашей стороне. Попробуйте немного позже.",
    image: square(img500),
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
 * The page a load's status deserves. 410 means deleted only where `goneMeansRemoved` opts in (topics),
 * elsewhere it reads as 404. A status without a page of its own gets the general one; none or a 5xx is ours.
 */
export function errorCodeForStatus(
  status: number | undefined,
  options: { goneMeansRemoved?: boolean } = {},
): number {
  if (!status || status >= 500) return 500;
  if (status === 410) return options.goneMeansRemoved ? 410 : 404;
  return status;
}
