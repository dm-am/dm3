/**
 * @vitest-environment node
 */

/**
 * The browser's own fields of a date, a time, a month or a week are nowhere
 * in the client: their look, language and order of parts are the browser's
 * and not the site's. A date is DateInput, a time TimeInput, the two together
 * DateTimeInput.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { join } from "path";
import { parse as parseSfc } from "vue/compiler-sfc";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

const isComponent = (name: string): boolean => name.endsWith(".vue");

const source = (file: string) => readFileSync(join(CLIENT_SRC, file), "utf8");

/** The markup of a single file component, comments out. */
const template = (file: string) =>
  (parseSfc(source(file)).descriptor.template?.content ?? "").replace(
    /<!--[\s\S]*?-->/g,
    "",
  );

describe("the browser's own date fields", () => {
  // A walk or a reading of the markup that came back empty would pass the
  // rule below without a word.
  it("reads the markup of the components, the date field among them", () => {
    const components = collect(CLIENT_SRC, isComponent).map(asPath);
    expect(components.length).toBeGreaterThan(250);
    expect(components).toContain("shared/ui/DatePicker/DateInput.vue");
    expect(template("shared/ui/DatePicker/DateInput.vue")).toMatch(
      /<input[^>]*type="text"/,
    );
  });

  it("are nowhere in the client", () => {
    const offenders: string[] = [];
    // The markup only: a comment may name the field it replaced.
    for (const file of collect(CLIENT_SRC, isComponent)) {
      for (const [field] of template(asPath(file)).matchAll(
        /<input[^>]*type="(?:date|time|datetime-local|month|week)"/g,
      ))
        offenders.push(`${asPath(file)}: ${field.replace(/\s+/g, " ")}`);
    }
    expect(offenders).toEqual([]);
  });
});
