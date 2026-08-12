/**
 * The utility answers two questions about a moderator-entered URL, and the
 * award popover renders whichever answer it gets: a router path, an anchor, or
 * the label with no link at all. Nothing validates the field on the way in -
 * the grant stores it trimmed and nothing else - so both answers are asserted
 * against values a moderator can really store.
 *
 * The origin is read from the same window the code reads rather than written
 * out, so the file does not depend on the address jsdom happens to serve.
 */
import { describe, expect, it } from "vitest";
import { toExternalHref, toInternalPath } from "./internalUrl";

const origin = window.location.origin;
const protocol = window.location.protocol;

describe("toInternalPath", () => {
  it("keeps a path of this site, query and fragment included", () => {
    expect(toInternalPath("/forum/general/1")).toBe("/forum/general/1");
    expect(toInternalPath("/forum/general/1?page=2#p3")).toBe(
      "/forum/general/1?page=2#p3",
    );
  });

  it("strips the origin when the URL is written out in full", () => {
    expect(toInternalPath(`${origin}/forum/general/1`)).toBe(
      "/forum/general/1",
    );
  });

  it("refuses the two spellings that start with a slash and leave the site", () => {
    // Both were handed back verbatim while a leading slash was trusted, and
    // both render an href that resolves to another host.
    expect(toInternalPath("//evil.example/x")).toBeNull();
    expect(toInternalPath("/\\evil.example/x")).toBeNull();
  });

  it("refuses anything that is not this origin", () => {
    expect(toInternalPath("https://evil.example/x")).toBeNull();
    expect(toInternalPath("javascript:alert(1)")).toBeNull();
    expect(toInternalPath("")).toBeNull();
    expect(toInternalPath(null)).toBeNull();
    expect(toInternalPath(undefined)).toBeNull();
  });

  it("returns what the parser resolved rather than the raw string", () => {
    // The one behaviour change for input that was always internal, and the
    // truer value for router.push.
    expect(toInternalPath("/forum/../blogs/1")).toBe("/blogs/1");
  });
});

describe("toExternalHref", () => {
  it("allows http and https", () => {
    expect(toExternalHref("https://example.com/topic/1")).toBe(
      "https://example.com/topic/1",
    );
    expect(toExternalHref("http://example.com/topic/1")).toBe(
      "http://example.com/topic/1",
    );
  });

  it("hands back a slash-leading foreign URL as the external link it is", () => {
    expect(toExternalHref("//evil.example/x")).toBe(
      `${protocol}//evil.example/x`,
    );
    expect(toExternalHref("/\\evil.example/x")).toBe(
      `${protocol}//evil.example/x`,
    );
  });

  it("refuses every other scheme, because the anchor would run it", () => {
    expect(toExternalHref("javascript:alert(1)")).toBeNull();
    expect(
      toExternalHref("data:text/html,<script>alert(1)</script>"),
    ).toBeNull();
    expect(toExternalHref("vbscript:msgbox(1)")).toBeNull();
    expect(toExternalHref("mailto:someone@example.com")).toBeNull();
  });

  it("refuses an internal value, which belongs to the router", () => {
    expect(toExternalHref("/forum/general/1")).toBeNull();
    expect(toExternalHref(`${origin}/forum/general/1`)).toBeNull();
    expect(toExternalHref(null)).toBeNull();
  });
});
