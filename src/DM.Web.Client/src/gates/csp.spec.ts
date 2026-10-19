/**
 * @vitest-environment node
 */

/**
 * script-src of the document allows no inline script, and the one inline script
 * the document has is named by hash: the directive is the second line of
 * defence on a site that binds server-rendered user HTML through v-html. The
 * hash is recomputed here from the file itself and compared with the one the
 * header carries, so a changed boot script fails here and not as a light flash
 * in production; the failure message hands over the value to paste.
 */
import { describe, it, expect } from "vitest";
import { createHash } from "crypto";
import { readFileSync } from "fs";
import { join } from "path";
import { CLIENT_SRC } from "@/shared/lib/testing/sourceTree";

// src -> client root, where index.html and nginx.conf live side by side
const CLIENT_ROOT = join(CLIENT_SRC, "..");

const indexHtml = readFileSync(join(CLIENT_ROOT, "index.html"), "utf8");
const nginxConf = readFileSync(join(CLIENT_ROOT, "nginx.conf"), "utf8");

/** One directive of the document policy, its name included. */
function policyDirective(name: string): string {
  const policy = nginxConf.match(/Content-Security-Policy\s+"([^"]+)"/);
  if (!policy) {
    throw new Error("nginx.conf declares no Content-Security-Policy");
  }

  const found = policy[1]
    .split(";")
    .map((part) => part.trim())
    .find((part) => part.startsWith(name));

  if (!found) {
    throw new Error(`the document policy declares no ${name}`);
  }

  return found;
}

/** The script-src directive of the document policy, name included. */
function scriptSrc(): string {
  return policyDirective("script-src");
}

/** Bodies of every inline script tag of the document, tags excluded. */
function inlineScripts(): string[] {
  return [...indexHtml.matchAll(/<script>([\s\S]*?)<\/script>/g)].map(
    (match) => match[1],
  );
}

describe("Content-Security-Policy of the document", () => {
  it("allows no inline script and no eval", () => {
    expect(scriptSrc()).not.toContain("unsafe-inline");
    expect(scriptSrc()).not.toContain("unsafe-eval");
  });

  it("names every inline script of index.html by its hash", () => {
    const scripts = inlineScripts();
    expect(scripts.length).toBeGreaterThan(0);

    const directive = scriptSrc();
    for (const body of scripts) {
      const hash = createHash("sha256").update(body, "utf8").digest("base64");
      expect(
        directive,
        `script-src must carry 'sha256-${hash}' for the inline script in index.html`,
      ).toContain(`'sha256-${hash}'`);
    }
  });

  it("opens a socket to this origin and to no other", () => {
    // ws: and wss: are scheme sources: they match every host there is, so the
    // one directive that bounds where a page may send data bounded nothing.
    // The hub is on the origin the document came from, and 'self' covers
    // ws:// and wss:// on that host and port: an http document reaches ws://
    // under it, and any document reaches wss://.
    const connectSrc = policyDirective("connect-src");

    expect(connectSrc).toContain("'self'");
    expect(connectSrc).not.toMatch(/\sws:/);
    expect(connectSrc).not.toMatch(/\swss:/);
  });
});
