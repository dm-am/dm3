/**
 * The cascade over a compiled sheet, which jsdom does not decide: the rules
 * that reach an element, the heavier selector winning and order breaking a
 * tie. Spec-only.
 */
import { outranks, specificity, styleRules, type Declarations } from "./styles";

/** Every rule of a compiled sheet that matches the element, in sheet order. */
export const matching = (css: string, element: Element) =>
  styleRules(css)
    .flatMap(({ selector, declarations }) =>
      selector
        .split(",")
        .map((one) => one.trim())
        .filter((one) => one && !one.includes("::"))
        .map((one) => ({ selector: one, declarations })),
    )
    .filter(({ selector }) => {
      try {
        return element.matches(selector);
      } catch {
        return false;
      }
    });

/**
 * What the sheet declares for the element, the heavier selector winning and
 * sheet order breaking a tie, as the cascade decides it.
 */
export const cascaded = (css: string, element: Element): Declarations => {
  const rules = matching(css, element)
    .map((rule, index) => ({
      ...rule,
      index,
      weight: specificity(rule.selector),
    }))
    .sort((a, b) =>
      outranks(a.weight, b.weight)
        ? 1
        : outranks(b.weight, a.weight)
          ? -1
          : a.index - b.index,
    );
  return Object.assign({}, ...rules.map((rule) => rule.declarations));
};
