/**
 * The markup of a tag description, character for character: what the server
 * renders for the hint surface (BbSurface.Hint) and what the moderator's
 * preview draws (hintToHtml), for the same input.
 *
 * The grammar table (bbcodeGrammar.cases.ts) states what a text is in neutral
 * pieces, and neither side's markup is in it. That leaves the names the two
 * sides give the span of a [tip] unchecked, and the tooltip wires itself to
 * those names: were they to drift apart, the small tooltip over a word would
 * open in the moderator's preview and not for a reader, or the other way
 * round, and no gate would see it. So these pairs are the markup itself, held
 * from both ends: HintMarkupShould renders each input on the server and
 * compares the string, hintMarkup.spec.ts does the same with hintToHtml, and
 * TooltipContent.spec.ts wires the tooltip to the markup of these pairs.
 *
 * Only what both draw the same is here. A hidden block is not: the page
 * erases it for every reader, and the preview shows the moderator the text the
 * refusal of the save is about.
 *
 * How the server reads it: strict JSON between the two marker comments, as
 * BbGrammarTable reads the grammar table. Keys and strings in double quotes,
 * no trailing comma, which is why the declaration carries prettier-ignore.
 */

export type HintMarkupPair = readonly [input: string, html: string];

// prettier-ignore
export const HINT_MARKUP_PAIRS: readonly HintMarkupPair[] = /* hint:begin */ [
["Erotic Role-Play: [tip img=\"/images/erp-tooltip.gif\"]эротические сцены[/tip] как основа игрового процесса", "Erotic Role-Play: <span class=\"bb-tip\" tabindex=\"0\" data-tip-img=\"/images/erp-tooltip.gif\">эротические сцены</span> как основа игрового процесса"],
["Игра затрагивает [tip text=\"Политика, религия, национальные конфликты\"]спорные[/tip] темы", "Игра затрагивает <span class=\"bb-tip\" tabindex=\"0\" data-tip-text=\"Политика, религия, национальные конфликты\">спорные</span> темы"],
["[tip img=\"https://example.com/a.gif\" text=\"Подпись\"]слово[/tip]", "<span class=\"bb-tip\" tabindex=\"0\" data-tip-text=\"Подпись\" data-tip-img=\"https://example.com/a.gif\">слово</span>"],
["[tip text=\"он сказал \"да\" & 'нет' <сразу>\"]a < b & c[/tip]", "<span class=\"bb-tip\" tabindex=\"0\" data-tip-text=\"он сказал &quot;да&quot; &amp; &#39;нет&#39; &lt;сразу&gt;\">a &lt; b &amp; c</span>"],
["[tip img=\" /images/a.gif \"]слово[/tip]", "<span class=\"bb-tip\" tabindex=\"0\" data-tip-img=\"/images/a.gif\">слово</span>"],
["[tip text=\" с пробелами \"] слово [/tip]", "<span class=\"bb-tip\" tabindex=\"0\" data-tip-text=\" с пробелами \"> слово </span>"],
["[TIP TEXT=\"подсказка\"]слово[/Tip]", "<span class=\"bb-tip\" tabindex=\"0\" data-tip-text=\"подсказка\">слово</span>"],
["[tip text=\"a b\"]c d[/tip]", "<span class=\"bb-tip\" tabindex=\"0\" data-tip-text=\"a b\">c d</span>"],
["[tip text=\"Ва‮ся\"]сло⁦во[/tip]", "<span class=\"bb-tip\" tabindex=\"0\" data-tip-text=\"Вася\">слово</span>"],
["[tip img=\"images/a.gif\"]a < b[/tip]", "[tip img=\"images/a.gif\"]a &lt; b[/tip]"],
["[tip]слово[/tip] и [tip text=\"\"]слово[/tip]", "[tip]слово[/tip] и [tip text=\"\"]слово[/tip]"],
["[tip text=\"раз\"]а [tip text=\"два\"]б[/tip][/tip]", "[tip text=\"раз\"]а <span class=\"bb-tip\" tabindex=\"0\" data-tip-text=\"два\">б</span>[/tip]"],
["раз\nдва\n\nтри", "раз<br />два<br /><br />три"],
["a < b & c > d -- e", "a &lt; b &amp; c &gt; d -- e"],
["[noparse][tip text=\"x\"]a < b[/tip]\n[/noparse]", "[tip text=\"x\"]a &lt; b[/tip]\n"],
["[b]жирный[/b] [tipimg:/a.gif]текст[/tipimg]", "[b]жирный[/b] [tipimg:/a.gif]текст[/tipimg]"]
] /* hint:end */;
