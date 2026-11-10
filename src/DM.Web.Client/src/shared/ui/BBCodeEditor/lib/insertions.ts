/**
 * The insertions that ask for something first: a link, an image, a hidden
 * block's readers and a spoiler's caption, each in a panel under its button
 * (InsertPanel.vue). Here are the fields of each panel, what a field cannot
 * hold and why, and what the panel writes into the text in either mode.
 */
import {
  isWritableValue,
  isWritablePrivateValue,
} from "@/shared/lib/bbcode/bbcodeSpelling";
import { isPixels, MAX_PIXELS } from "@/shared/lib/bbcode/bbcodeTagSet";
import type { InputField } from "../InsertPanel.vue";
import { insertAtCaret, wrapSelection, type EditorTarget } from "./sourceField";

/** The insertions that ask for something first, each in a panel under its button. */
export type InsertKind = "link" | "image" | "private" | "spoiler";

/**
 * Why a parameter value cannot be written, or null when it can. Markup has no
 * escaping, so the value would end sooner than it was meant to.
 */
const refusalBy =
  (writable: (value: string) => boolean) =>
  (value: string): string | null => {
    if (writable(value)) return null;
    if (value.includes("[")) return "Нельзя использовать символ [";
    if (/" *\]/.test(value)) return "Нельзя ставить кавычку перед ]";
    return "Нельзя ставить кавычку перед словом со знаком =";
  };

const valueRefusal = refusalBy(isWritableValue);

/** A size in pixels, as the grammar takes it. */
function sizeRefusal(value: string): string | null {
  if (!value.trim() || isPixels(value.trim())) return null;
  return `Только целое число от 1 до ${MAX_PIXELS}`;
}

// The fields of the insertion panels. The text of a link comes first, as it
// reads in the post, and the caret starts in it; the address is the one field
// a link cannot do without.
const linkFields: InputField[] = [
  {
    name: "text",
    label: "Текст ссылки",
    placeholder: "Отображаемый текст",
    validator: valueRefusal,
  },
  {
    name: "url",
    label: "URL ссылки",
    type: "url",
    placeholder: "https://example.com",
    required: true,
  },
];

const imageFields: InputField[] = [
  {
    name: "url",
    label: "URL изображения",
    type: "url",
    placeholder: "https://example.com/image.jpg",
    required: true,
  },
  {
    name: "width",
    label: "Ширина (px)",
    placeholder: "600",
    validator: sizeRefusal,
  },
  {
    name: "height",
    label: "Высота (px)",
    placeholder: "400",
    validator: sizeRefusal,
  },
];

// Empty names make the bare [private], which the game leads alone read.
const privateFields: InputField[] = [
  {
    name: "character",
    label: "Имена персонажей через запятую",
    placeholder: "Вася, Петя",
    validator: refusalBy(isWritablePrivateValue),
  },
];

const spoilerFields: InputField[] = [
  {
    name: "text",
    label: "Подпись кнопки",
    placeholder: "Показать содержимое",
    validator: valueRefusal,
  },
];

/**
 * [link]address[/link] shows the word "ссылка", as on DM2;
 * [link text="..."]address[/link] shows its own text.
 */
function handleLinkSubmit(
  target: EditorTarget,
  values: Record<string, string>,
) {
  const url = values.url?.trim();
  const text = values.text?.trim();

  if (!url) return;

  if (target.mode.value === "bbcode") {
    insertAtCaret(
      target,
      text ? `[link text="${text}"]${url}[/link]` : `[link]${url}[/link]`,
    );
  } else {
    target.editor.value
      ?.chain()
      .focus()
      .setBbLink({ href: url, text: text || undefined })
      .run();
  }
}

/**
 * [img width="..." height="..."]address[/img], with only the parameters that
 * were filled in, in that order. An image has no alt: a reader never sees it,
 * and text nobody sees on the page is where an insult goes unnoticed.
 */
function handleImageSubmit(
  target: EditorTarget,
  values: Record<string, string>,
) {
  const url = values.url?.trim();
  if (!url) return;

  const width = values.width?.trim() || null;
  const height = values.height?.trim() || null;

  if (target.mode.value === "bbcode") {
    let opening = "[img";
    if (width) opening += ` width="${width}"`;
    if (height) opening += ` height="${height}"`;
    insertAtCaret(target, `${opening}]${url}[/img]`);
  } else {
    target.editor.value
      ?.chain()
      .focus()
      .setBbImage({
        src: url,
        width: width ? parseInt(width, 10) : null,
        height: height ? parseInt(height, 10) : null,
      })
      .run();
  }
}

function handleSpoilerSubmit(
  target: EditorTarget,
  values: Record<string, string>,
) {
  const text = values.text?.trim();
  if (target.mode.value === "bbcode") {
    wrapSelection(
      target,
      text ? `[spoiler text="${text}"]` : "[spoiler]",
      "[/spoiler]",
    );
  } else {
    target.editor.value
      ?.chain()
      .focus()
      .setSpoiler({ text: text || null })
      .run();
  }
}

/**
 * [private="Вася, Петя"] for the characters named, and the bare [private],
 * which only the game leads read, when no name is given.
 */
function handlePrivateSubmit(
  target: EditorTarget,
  values: Record<string, string>,
) {
  const character = values.character?.trim() || null;

  if (target.mode.value === "bbcode") {
    wrapSelection(
      target,
      character === null ? "[private]" : `[private="${character}"]`,
      "[/private]",
    );
  } else {
    target.editor.value?.chain().focus().togglePrivate({ character }).run();
  }
}

/** Each panel: its name, which is its button's, its fields and what it writes. */
export const INSERTIONS: Record<
  InsertKind,
  {
    label: string;
    fields: InputField[];
    write: (target: EditorTarget, values: Record<string, string>) => void;
  }
> = {
  link: {
    label: "Вставить ссылку",
    fields: linkFields,
    write: handleLinkSubmit,
  },
  image: {
    label: "Вставить изображение",
    fields: imageFields,
    write: handleImageSubmit,
  },
  private: {
    label: "Скрытый блок",
    fields: privateFields,
    write: handlePrivateSubmit,
  },
  spoiler: {
    label: "Спойлер",
    fields: spoilerFields,
    write: handleSpoilerSubmit,
  },
};
