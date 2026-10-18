/**
 * What the help of the markup editor says: every tag a surface reads, in the
 * order of the toolbar, each with what the author types and what a reader gets.
 *
 * The typed half is markup the grammar accepts as written (BBCODE_RENDERING.md,
 * "Грамматика разметки"): the forms of DM2 and the parameters added over them,
 * with lower-case names, as the editor writes them itself. The shown half is
 * the markup the server sends a reader for it (BbParserProvider and the tags
 * under Parsing/Tags), drawn by the same global .bbcode-content styles a post
 * is drawn with, so the help shows the page and not a picture of it. The
 * preview of the editor is not used for it: it draws a spoiler open and
 * without its caption, and a hidden block without the recipients the tooltip
 * over it names.
 *
 * markupHelp.spec.ts holds both halves to the grammar: every typed example is
 * markup the check passes in silence, and every shown one says the words the
 * preview of that markup says.
 */

/** A chord of the editor: Ctrl (Cmd on a Mac), maybe Shift, and a letter. */
export interface HelpChord {
  letter: string;
  shift?: boolean;
}

/** One example: what is typed, and what a reader gets for it. */
export interface HelpExample {
  typed: string;
  /** The server's reading markup for `typed`, as HTML. */
  shown: string;
}

/** A row of the help: a tag, its examples, its keys and a word about it. */
export interface HelpEntry {
  /** The tag's name, as its button on the toolbar says it. */
  tag: string;
  examples: readonly HelpExample[];
  /**
   * The chord the editor binds to the tag in both of its modes. A chord the
   * visual mode alone knows is not named: in the BBCode mode it does nothing.
   */
  keys?: HelpChord;
  /** What the examples do not show by themselves. */
  note?: string;
}

/** The address every example of an image and a link points at. */
const EXAMPLE_ADDRESS = "https://example.com/";

/**
 * The pictures shown for an image. The addresses of the examples are made up,
 * and a made-up address draws nothing, so the results are the site's own d20,
 * served from the site itself: a copy of it at 32px shown at its own size, and
 * the large one held to the size its example names. Shown at its own 192px the
 * large one took a screen's worth of the table for one line of markup. The
 * small one is not the favicon: a page that names /favicon.ico as its icon
 * does not always load it as a picture, and the first example showed an empty
 * cell.
 */
const SMALL_PICTURE = "/images/markup-help-d20.png";
const LARGE_PICTURE = "/apple-touch-icon.png";

// A captioned spoiler keeps its caption on the open button, as the server
// draws it; one without a caption swaps to the old site's words.
const spoiler = (content: string, caption?: string) =>
  `<a href="#" class="spoiler-head" data-swaptext="${caption ?? "Скрыть содержимое"}">${caption ?? "Показать содержимое"}</a>` +
  `<div class="spoiler">${content}</div>`;

const nsfw = (content: string) =>
  `<a href="#" class="nsfw-head" data-swaptext="Скрыть шокирующий контент">Показать шокирующий контент</a>` +
  `<div class="nsfw-spoiler">${content}</div>`;

/** The leads, whom the readers of every hidden block start with. */
const LEADS = "DungeonMaster, Assistant";

/**
 * The green text, its readers on it for the tooltip: the leads, then the
 * names, if any.
 */
const hidden = (content: string, names?: string) =>
  `<span class="private-message" data-recipients="${names ? `${LEADS}, ${names}` : LEADS}">${content}</span>`;

const image = (picture: string, width?: number) => {
  const img =
    `<img src="${picture}" class="bb-image" data-bb-tag="img" ` +
    `decoding="async" referrerpolicy="no-referrer" alt="" />`;
  return width === undefined
    ? img
    : `<span class="bb-image-frame" data-bb-width="${width}" style="--bb-image-max-width:${width}px">${img}</span>`;
};

const link = (text: string) =>
  `<a href="${EXAMPLE_ADDRESS}" target="_blank" rel="noopener">${text}</a>`;

const list = (element: "ul" | "ol") =>
  `<${element}><li>первый пункт</li><li>второй пункт</li></${element}>`;

const typedList = (tag: "ul" | "ol") =>
  `[${tag}]\n[li]первый пункт[/li]\n[li]второй пункт[/li]\n[/${tag}]`;

export const HELP_ENTRIES: readonly HelpEntry[] = [
  {
    tag: "b",
    keys: { letter: "b" },
    examples: [
      { typed: "[b]жирный текст[/b]", shown: "<strong>жирный текст</strong>" },
    ],
  },
  {
    tag: "i",
    keys: { letter: "i" },
    examples: [{ typed: "[i]курсив[/i]", shown: "<em>курсив</em>" }],
  },
  {
    tag: "u",
    keys: { letter: "u" },
    examples: [
      {
        typed: "[u]подчеркнутый текст[/u]",
        shown: "<u>подчеркнутый текст</u>",
      },
    ],
  },
  {
    tag: "strike",
    examples: [
      {
        typed: "[strike]зачеркнутый текст[/strike]",
        shown: "<s>зачеркнутый текст</s>",
      },
    ],
  },
  {
    tag: "spoiler",
    keys: { letter: "s", shift: true },
    examples: [
      {
        typed: "[spoiler]скрытый текст[/spoiler]",
        shown: spoiler("скрытый текст"),
      },
      {
        typed: '[spoiler text="Карта"]скрытый текст[/spoiler]',
        shown: spoiler("скрытый текст", "Карта"),
      },
    ],
    note: 'Параметр text заменяет надпись "Показать содержимое"',
  },
  {
    tag: "nsfw",
    examples: [
      {
        typed: "[nsfw]текст с шокирующим содержимым[/nsfw]",
        shown: nsfw("текст с шокирующим содержимым"),
      },
    ],
  },
  {
    tag: "private",
    examples: [
      {
        typed: '[private="Вася, Петя"]тайна[/private]',
        shown: hidden("тайна", "Вася, Петя"),
      },
      {
        typed: "[private]тайна[/private]",
        shown: hidden("тайна"),
      },
    ],
    note:
      "Имена персонажей пишутся через запятую, текст видят их игроки и автор поста. " +
      "DungeonMaster и Assistant видят любой скрытый текст игры, в том числе без имен. " +
      "Кому виден текст, показывает подсказка: наведите мышь или коснитесь зеленого текста.",
  },
  {
    tag: "ul",
    examples: [{ typed: typedList("ul"), shown: list("ul") }],
  },
  {
    tag: "ol",
    examples: [{ typed: typedList("ol"), shown: list("ol") }],
  },
  {
    tag: "img",
    examples: [
      {
        typed: `[img]${EXAMPLE_ADDRESS}icon.png[/img]`,
        shown: image(SMALL_PICTURE),
      },
      {
        typed: `[img width="60"]${EXAMPLE_ADDRESS}d20.png[/img]`,
        shown: image(LARGE_PICTURE, 60),
      },
    ],
    note:
      "Адрес начинается с http:// или https://. Параметры width и height задают " +
      "наибольший размер в пикселях, от 1 до 9999, и картинка сохраняет пропорции. " +
      "Без height картинка не выше 500 пикселей, щелчок по ней открывает ее целиком.",
  },
  {
    tag: "link",
    examples: [
      { typed: `[link]${EXAMPLE_ADDRESS}[/link]`, shown: link("ссылка") },
      {
        typed: `[link text="Главная"]${EXAMPLE_ADDRESS}[/link]`,
        shown: link("Главная"),
      },
    ],
    note: 'Адрес начинается с http:// или https://, параметр text заменяет слово "ссылка"',
  },
  {
    tag: "mention",
    examples: [
      {
        typed: "[mention]Логин[/mention]",
        shown:
          `<a class="bb-mention" href="/users/${encodeURIComponent("Логин")}" ` +
          `data-bb-tag="mention" data-bb-user="Логин">@Логин</a>`,
      },
    ],
    note: "Ссылка на профиль пользователя с этим логином",
  },
  {
    tag: "quote",
    examples: [
      {
        typed: "[quote]высказывание[/quote]",
        shown: '<div class="quote">высказывание</div>',
      },
    ],
  },
  {
    tag: "mod",
    examples: [
      {
        typed: "[mod]сообщение модератора[/mod]",
        shown: '<div class="mod-block">сообщение модератора</div>',
      },
    ],
    note: "Видят все читатели, пишут только модераторы",
  },
  {
    tag: "tab",
    examples: [
      {
        typed: "[tab]Начало абзаца",
        shown: "&nbsp;&nbsp;&nbsp;Начало абзаца",
      },
    ],
    note: "Небольшой отступ, закрывающего тега у него нет",
  },
  {
    tag: "code",
    examples: [
      {
        typed: "[code]1d20 + [b]сила[/b][/code]",
        shown: '<code class="code">1d20 + <strong>сила</strong></code>',
      },
    ],
    note: "Меняет только шрифт: теги внутри работают, пробелы и переводы строк сохраняются",
  },
  {
    tag: "noparse",
    examples: [
      {
        typed: "[noparse][b]не жирный[/b][/noparse]",
        shown: "[b]не жирный[/b]",
      },
      {
        typed: "[code][noparse][b]пример[/b][/noparse][/code]",
        shown: '<code class="code">[b]пример[/b]</code>',
      },
    ],
    note: "Показывает все внутри как набрано, так пишут пример разметки",
  },
];

/** A chord as the help prints it: "Ctrl+B", "Ctrl+Shift+S". */
export function chordLabel(chord: HelpChord): string {
  return `Ctrl+${chord.shift ? "Shift+" : ""}${chord.letter.toUpperCase()}`;
}

/**
 * Typed markup in pieces that end after each closing bracket. The help puts a
 * break opportunity between them, so a long example wraps between two tags and
 * not in the middle of an address; a copy of it is still one line.
 */
export function typedPieces(typed: string): string[] {
  return typed.split(/(?<=\])/);
}
