import type { Reporter, TestCase, TestResult } from "@playwright/test/reporter";
import { mkdirSync, writeFileSync } from "fs";
import { join } from "path";

interface Row {
  state: string;
  project: string;
  status: string;
  differs: string[];
  error?: string;
}

/**
 * The report of a diff run, by state: which of the three channels moved, and
 * which states could not be reached at all (a selector of the stand no longer
 * finds its element). Written as text for a person and as JSON for a script;
 * the frames, the actual trees and their diffs lie next to it.
 */
export default class FramesReporter implements Reporter {
  private readonly rows: Row[] = [];

  constructor(private readonly options: { report: string; diffing: boolean }) {}

  onTestEnd(test: TestCase, result: TestResult) {
    const differs = result.annotations
      .filter((a) => a.type === "differs")
      .map((a) => a.description ?? "");
    const unexplained =
      result.status !== "passed" &&
      result.status !== "skipped" &&
      differs.length === 0;
    this.rows.push({
      state: test.title,
      project: test.parent.project()?.name ?? "",
      status: result.status,
      differs,
      error: unexplained
        ? (result.errors[0]?.message ?? "").split("\n")[0]
        : undefined,
    });
  }

  onEnd() {
    const changed = this.rows.filter((r) => r.differs.length > 0);
    const broken = this.rows.filter((r) => r.error !== undefined);
    if (!this.options.diffing) {
      // A snapshot with a state it could not reach is not a base: the run
      // fails, and these are why.
      console.log(
        `\nstates taken: ${this.rows.filter((r) => r.status === "passed").length}, not reached: ${broken.length}`,
      );
      for (const r of broken)
        console.log(`BROKEN   ${r.project.padEnd(8)} ${r.state}: ${r.error}`);
      return;
    }
    const same = this.rows.filter((r) => r.status === "passed").length;
    const lines = [
      `states compared: ${this.rows.filter((r) => r.status !== "skipped").length}, same: ${same}, differ: ${changed.length}, not reached: ${broken.length}`,
      "",
      ...changed.map(
        (r) =>
          `DIFFERS  ${r.project.padEnd(8)} ${r.state}: ${r.differs.join(", ")}`,
      ),
      ...broken.map(
        (r) => `BROKEN   ${r.project.padEnd(8)} ${r.state}: ${r.error}`,
      ),
    ];
    mkdirSync(this.options.report, { recursive: true });
    writeFileSync(
      join(this.options.report, "report.txt"),
      `${lines.join("\n")}\n`,
    );
    writeFileSync(
      join(this.options.report, "report.json"),
      `${JSON.stringify(this.rows, null, 2)}\n`,
    );
    console.log(`\n${lines.join("\n")}`);
  }
}
