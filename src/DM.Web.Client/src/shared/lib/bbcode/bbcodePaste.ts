/**
 * Cleans HTML pasted from Word, Google Docs and other rich editors: junk markup
 * goes, the formatting tags BBCode can carry stay.
 *
 * @param html - Raw pasted HTML
 * @returns Cleaned HTML suitable for conversion
 *
 * @example
 * ```typescript
 * cleanPastedHtml('<p class="MsoNormal" style="font-size:12pt">Hello</p>')
 * // => 'Hello\n'
 * ```
 */
export function cleanPastedHtml(html: string): string {
  if (!html) return "";

  let cleaned = html;

  // Remove Microsoft Office specific tags and namespaces
  cleaned = cleaned.replace(/<o:p[^>]*>[\s\S]*?<\/o:p>/gi, "");
  cleaned = cleaned.replace(/<w:[^>]+>[\s\S]*?<\/w:[^>]+>/gi, "");
  cleaned = cleaned.replace(/<m:[^>]+>[\s\S]*?<\/m:[^>]+>/gi, "");
  cleaned = cleaned.replace(/<!--\[if[^>]*>[\s\S]*?<!\[endif\]-->/gi, "");
  cleaned = cleaned.replace(/<!--[\s\S]*?-->/g, "");

  // Remove XML declarations and processing instructions
  cleaned = cleaned.replace(/<\?xml[^>]*>/gi, "");
  cleaned = cleaned.replace(/<!\[CDATA\[[\s\S]*?\]\]>/gi, "");

  // Remove style, script, and meta tags
  cleaned = cleaned.replace(/<style[^>]*>[\s\S]*?<\/style>/gi, "");
  cleaned = cleaned.replace(/<script[^>]*>[\s\S]*?<\/script>/gi, "");
  cleaned = cleaned.replace(/<meta[^>]*\/?>/gi, "");
  cleaned = cleaned.replace(/<link[^>]*\/?>/gi, "");
  cleaned = cleaned.replace(/<title[^>]*>[\s\S]*?<\/title>/gi, "");
  cleaned = cleaned.replace(/<head[^>]*>[\s\S]*?<\/head>/gi, "");
  cleaned = cleaned.replace(/<html[^>]*>/gi, "");
  cleaned = cleaned.replace(/<\/html>/gi, "");
  cleaned = cleaned.replace(/<body[^>]*>/gi, "");
  cleaned = cleaned.replace(/<\/body>/gi, "");

  // Remove all inline styles
  cleaned = cleaned.replace(/\s*style="[^"]*"/gi, "");
  cleaned = cleaned.replace(/\s*class="[^"]*"/gi, "");
  cleaned = cleaned.replace(/\s*id="[^"]*"/gi, "");

  // Remove font tags (deprecated HTML)
  cleaned = cleaned.replace(/<font[^>]*>/gi, "");
  cleaned = cleaned.replace(/<\/font>/gi, "");

  // Remove empty spans
  cleaned = cleaned.replace(/<span[^>]*>\s*<\/span>/gi, "");
  // Unwrap non-empty spans
  cleaned = cleaned.replace(/<span[^>]*>([\s\S]*?)<\/span>/gi, "$1");

  // Remove div wrappers
  cleaned = cleaned.replace(/<div[^>]*>([\s\S]*?)<\/div>/gi, "$1\n");

  // Normalize whitespace. A document pasted from elsewhere spaces its words
  // with no-break spaces as often as not, and the way back keeps a no-break
  // space as typed, so here, on the way in, they become plain ones.
  cleaned = cleaned.replace(/&nbsp;|\u00a0/gi, " ");
  cleaned = cleaned.replace(/\r\n/g, "\n");
  cleaned = cleaned.replace(/\r/g, "\n");
  cleaned = cleaned.replace(/\t/g, " ");
  cleaned = cleaned.replace(/ {2,}/g, " ");
  cleaned = cleaned.replace(/\n{3,}/g, "\n\n");

  // Remove zero-width characters
  cleaned = cleaned.replace(/[\u200B-\u200D\uFEFF]/g, "");

  return cleaned.trim();
}
