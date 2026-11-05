import { CONTEXT_TAGS } from "./bbcodeTagSet";
import { asTheServerReads } from "./bbcodeSpelling";
import { readText } from "./bbcodeReader";
import { escapeText } from "./bbcodeToHtml";

/** A value inside a quoted attribute, encoded as the server's TipTag encodes it. */
function tipAttr(name: string, value: string | null): string {
  if (value === null) return "";
  const encoded = escapeText(value)
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");
  return ` ${name}="${encoded}"`;
}

/**
 * A game tag's description as the server renders it for the hint surface
 * (BbSurface.Hint), character for character, held to it by hintMarkup.cases.ts.
 * No typography; a hidden block shows as typed, since the save refuses it.
 */
export function hintToHtml(bbcode: string): string {
  if (!bbcode) return "";
  const nodes = readText(
    asTheServerReads(bbcode),
    new Set(CONTEXT_TAGS.hint),
    null,
  );
  return nodes
    .map((node) => {
      switch (node.t) {
        case "text":
          return escapeText(node.s).replace(/\r?\n/g, "<br />");
        case "verbatim":
          return escapeText(node.s);
        case "tip":
          return (
            `<span class="bb-tip" tabindex="0"` +
            `${tipAttr("data-tip-text", node.text)}${tipAttr("data-tip-img", node.img)}>` +
            `${escapeText(node.word)}</span>`
          );
        default:
          // The hint reads [tip] and [noparse] and nothing else, so no other
          // node comes out of the reader.
          throw new Error(`the hint read a node it has no tag for: ${node.t}`);
      }
    })
    .join("");
}
