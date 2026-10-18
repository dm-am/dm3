/**
 * @vitest-environment node
 */

/**
 * The browser's own fields of a date, a time, a month or a week are nowhere
 * in the client: their look, language and order of parts are the browser's
 * and not the site's (the polls showed "06-Oct-2026 05:30 AM" with a calendar
 * of the browser's own). A date is DateInput, a time TimeInput, the two
 * together DateTimeInput.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";

const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

const asPath = (file: string): string =>
  relative(CLIENT_SRC, file).split("\\").join("/");

function collect(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) collect(full, out);
    } else if (/\.(vue|sass|ts)$/.test(name) && !name.endsWith(".spec.ts")) {
      out.push(full);
    }
  }
  return out;
}

const source = (file: string) => readFileSync(join(CLIENT_SRC, file), "utf8");

/** The markup of a single file component, comments out. */
const template = (file: string) =>
  (/<template>([\s\S]*)<\/template>/.exec(source(file))?.[1] ?? "").replace(
    /<!--[\s\S]*?-->/g,
    "",
  );

describe("the browser's own date fields", () => {
  // A walk or a reading of the markup that came back empty would pass the
  // rule below without a word.
  it("reads the markup of the components, the date field among them", () => {
    const components = collect(CLIENT_SRC)
      .filter((file) => file.endsWith(".vue"))
      .map(asPath);
    expect(components.length).toBeGreaterThan(250);
    expect(components).toContain("shared/ui/DatePicker/DateInput.vue");
    expect(template("shared/ui/DatePicker/DateInput.vue")).toMatch(
      /<input[^>]*type="text"/,
    );
  });

  it("are nowhere in the client", () => {
    const offenders: string[] = [];
    // The markup only: a comment may name the field it replaced.
    for (const file of collect(CLIENT_SRC).filter((f) => f.endsWith(".vue"))) {
      for (const [field] of template(asPath(file)).matchAll(
        /<input[^>]*type="(?:date|time|datetime-local|month|week)"/g,
      ))
        offenders.push(`${asPath(file)}: ${field.replace(/\s+/g, " ")}`);
    }
    expect(offenders).toEqual([]);
  });
});
