/** The key of the attribute on a node: data-bb-glue-after is glueAfter. */
export function attributeKey(name: string): string {
  return name
    .replace(/^data-bb-/, "")
    .replace(/-([a-z])/g, (_match, letter: string) => letter.toUpperCase());
}

/** An attribute read from and written to one data-* attribute, as a string. */
export function kept(name: string) {
  return {
    default: null,
    parseHTML: (element: HTMLElement) => element.getAttribute(name),
    renderHTML: (attributes: Record<string, unknown>) => {
      const value = attributes[attributeKey(name)];
      return typeof value === "string" ? { [name]: value } : {};
    },
  };
}
