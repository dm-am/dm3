/**
 * [link], [img] and the check of an address (sanitizeUrl)
 * on every way into and out of the preview: BBCODE_RENDERING.md, "Правило
 * безопасного отказа".
 */

import { describe, it, expect } from "vitest";
import { bbcodeToHtml } from "./bbcodeToHtml";
import { htmlToBbcode } from "./htmlToBbcode";
import { addressRefusal, sanitizeUrl } from "./bbcodeAddresses";
import { expectRoundTrip } from "@/shared/lib/testing/converterRoundTrip";

describe("BBCode Links", () => {
  describe('[link text="..."]URL[/link] - its own text', () => {
    it("converts to HTML with custom text displayed", () => {
      const bbcode = '[link text="Click here"]https://example.com[/link]';
      const html = bbcodeToHtml(bbcode);
      expect(html).toContain('data-bb-tag="link"');
      expect(html).toContain('data-bb-text="Click here"');
      expect(html).toContain(">Click here<");
      expect(html).toContain('href="https://example.com"');
    });

    it("survives round-trip", () => {
      expectRoundTrip('[link text="Click here"]https://example.com[/link]');
    });

    // [link=text] was a spelling of DM3's own, never of DM2: it is a broken
    // tag now, shown as typed, and the editor gives it back as typed.
    it("leaves the old [link=text] as the text it is", () => {
      const bbcode = "[link=Click here]https://example.com[/link]";
      expect(bbcodeToHtml(bbcode)).toBe(`<p>${bbcode}</p>`);
      expectRoundTrip(bbcode);
    });
  });

  describe('[link]URL[/link] - displays "ссылка"', () => {
    it('displays "ссылка" instead of URL', () => {
      const bbcode = "[link]https://example.com[/link]";
      const html = bbcodeToHtml(bbcode);
      expect(html).toContain('data-bb-selfref="true"');
      expect(html).toContain(">ссылка<");
      expect(html).toContain('href="https://example.com"');
    });

    it("survives round-trip", () => {
      const bbcode = "[link]https://example.com[/link]";
      const html = bbcodeToHtml(bbcode);
      expect(htmlToBbcode(html)).toBe(bbcode);
    });
  });

  describe("link attributes", () => {
    it('adds target="_blank" and rel="noopener"', () => {
      const html = bbcodeToHtml('[link text="text"]https://example.com[/link]');
      expect(html).toContain('target="_blank"');
      expect(html).toContain('rel="noopener"');
    });
  });
});

describe("BBCode Images", () => {
  describe("[img]URL[/img] DM2 format", () => {
    it("converts to HTML with marker", () => {
      const html = bbcodeToHtml("[img]https://example.com/image.png[/img]");
      expect(html).toContain('data-bb-tag="img"');
      expect(html).toContain('src="https://example.com/image.png"');
    });

    it("survives round-trip", () => {
      expectRoundTrip("[img]https://example.com/photo.jpg[/img]");
    });

    it("adds empty alt for accessibility", () => {
      const html = bbcodeToHtml("[img]https://example.com/photo.jpg[/img]");
      expect(html).toContain('alt=""');
    });

    it("handles URL with special characters", () => {
      const html = bbcodeToHtml(
        "[img]https://example.com/photo.jpg?size=large&format=webp[/img]",
      );
      expect(html).toContain(
        'src="https://example.com/photo.jpg?size=large&amp;format=webp"',
      );
    });

    it("converts unmarked images to [img]URL[/img] format", () => {
      const result = htmlToBbcode(
        '<img src="https://example.com/photo.jpg" />',
      );
      expect(result).toBe("[img]https://example.com/photo.jpg[/img]");
    });

    it("handles unmarked images with alt (ignores alt)", () => {
      const result = htmlToBbcode(
        '<img src="https://example.com/cat.jpg" alt="A cute cat" />',
      );
      expect(result).toBe("[img]https://example.com/cat.jpg[/img]");
    });
  });
});

describe("XSS Prevention - sanitizeUrl", () => {
  describe("dangerous protocols", () => {
    it("blocks javascript: protocol", () => {
      expect(sanitizeUrl("javascript:alert(1)")).toBe("#");
    });

    it("blocks javascript: with mixed case", () => {
      expect(sanitizeUrl("JaVaScRiPt:alert(1)")).toBe("#");
    });

    it("blocks javascript: with leading spaces", () => {
      expect(sanitizeUrl("  javascript:alert(1)")).toBe("#");
    });

    it("blocks data: protocol", () => {
      expect(sanitizeUrl("data:text/html,<script>alert(1)</script>")).toBe("#");
    });

    it("blocks data: base64 encoded", () => {
      expect(
        sanitizeUrl(
          "data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==",
        ),
      ).toBe("#");
    });

    it("blocks vbscript: protocol", () => {
      expect(sanitizeUrl('vbscript:msgbox("XSS")')).toBe("#");
    });
  });

  describe("safe protocols", () => {
    it("allows http: links", () => {
      expect(sanitizeUrl("http://example.com")).toBe("http://example.com");
    });

    it("allows https: links", () => {
      expect(sanitizeUrl("https://example.com")).toBe("https://example.com");
    });
  });

  // The server shows a link at http and https only (BbParserWrapper): an
  // address it drops must not pass here and vanish after the save.
  describe("addresses the server drops", () => {
    it.each([
      "mailto:user@example.com",
      "tel:+1234567890",
      "//example.com/path",
      "/path/to/page",
      "path/to/page",
      "https://exa mple.com/",
    ])("refuses %s", (url) => {
      expect(sanitizeUrl(url)).toBe("#");
    });

    // The reader's browser requests the address, so a post must not point it
    // at the reader's own machine or network.
    it.each([
      "http://localhost/",
      "http://a.localhost/",
      "http://127.0.0.1/",
      "http://127.1/",
      "http://2130706433/",
      "http://10.0.0.1/",
      "http://100.64.0.1/",
      "http://169.254.169.254/latest",
      "http://172.16.0.1/",
      "http://192.168.0.1/",
      "http://0.0.0.0/",
      "http://[::1]/",
      "http://[::ffff:127.0.0.1]/",
      "http://[fe80::1]/",
      "http://[fd00::1]/",
    ])("refuses the reader's own network: %s", (url) => {
      expect(sanitizeUrl(url)).toBe("#");
    });

    it.each([
      "http://8.8.8.8/",
      "http://172.32.0.1/",
      "http://[2001:db8::1]/",
      "https://example.com/",
    ])("lets a public address through: %s", (url) => {
      expect(sanitizeUrl(url)).toBe(url);
    });
  });

  describe("unsafe protocols blocked", () => {
    it("blocks ftp: protocol", () => {
      expect(sanitizeUrl("ftp://files.example.com")).toBe("#");
    });

    it("blocks file: protocol", () => {
      expect(sanitizeUrl("file:///etc/passwd")).toBe("#");
    });

    it("blocks custom protocols", () => {
      expect(sanitizeUrl("custom://something")).toBe("#");
    });
  });

  describe("edge cases", () => {
    it("handles empty string", () => {
      expect(sanitizeUrl("")).toBe("#");
    });

    it("handles URL with query params", () => {
      expect(sanitizeUrl("https://example.com?a=1&b=2")).toBe(
        "https://example.com?a=1&b=2",
      );
    });

    it("handles URL with hash", () => {
      expect(sanitizeUrl("https://example.com#section")).toBe(
        "https://example.com#section",
      );
    });

    it("trims whitespace", () => {
      expect(sanitizeUrl("  https://example.com  ")).toBe(
        "https://example.com",
      );
    });
  });
});

/**
 * An address the server refuses draws no image and no link: the tag is printed
 * as typed, its address in place, as the reader gets it. The author sees what
 * did not work, and the text comes back unchanged.
 */
describe("XSS Prevention - bbcodeToHtml", () => {
  /** Every element of the preview, and every address one of them carries. */
  const drawn = (bbcode: string) => {
    const template = document.createElement("template");
    template.innerHTML = bbcodeToHtml(bbcode);
    const elements = [...template.content.querySelectorAll("*")];
    return {
      tags: elements.map((element) => element.tagName),
      addresses: elements.flatMap((element) =>
        ["href", "src"].flatMap((name) => element.getAttribute(name) ?? []),
      ),
      text: template.content.textContent,
    };
  };

  describe("[link] XSS prevention", () => {
    it("draws no link for javascript: and prints the tag as typed", () => {
      const bbcode = '[link text="click me"]javascript:alert(1)[/link]';
      const { tags, addresses, text } = drawn(bbcode);
      expect(tags).not.toContain("A");
      expect(addresses).toEqual([]);
      expect(text).toBe(bbcode);
      expectRoundTrip(bbcode);
    });

    it("prints a link without text as typed for javascript:", () => {
      const bbcode = "[link]javascript:alert(1)[/link]";
      const { tags, addresses, text } = drawn(bbcode);
      expect(tags).not.toContain("A");
      expect(addresses).toEqual([]);
      expect(text).toBe(bbcode);
      expectRoundTrip(bbcode);
    });

    it("preserves safe links", () => {
      const html = bbcodeToHtml('[link text="safe"]https://example.com[/link]');
      expect(html).toContain('href="https://example.com"');
    });
  });

  describe("[img]URL[/img] XSS prevention (DM2 format)", () => {
    it("draws no image for javascript: and prints the tag as typed", () => {
      const bbcode = "[img]javascript:alert(1)[/img]";
      const { tags, addresses, text } = drawn(bbcode);
      expect(tags).not.toContain("IMG");
      expect(addresses).toEqual([]);
      expect(text).toBe(bbcode);
      expectRoundTrip(bbcode);
    });

    it("draws no image for data:", () => {
      const { tags, addresses } = drawn(
        '[img]data:image/svg+xml,<svg onload="alert(1)"/>[/img]',
      );
      expect(tags).not.toContain("IMG");
      expect(addresses).toEqual([]);
    });

    it("preserves safe image URLs", () => {
      const html = bbcodeToHtml("[img]https://example.com/photo.jpg[/img]");
      expect(html).toContain('src="https://example.com/photo.jpg"');
    });
  });
});

describe("XSS Prevention - htmlToBbcode", () => {
  describe("unmarked link XSS prevention", () => {
    it("sanitizes javascript: links from external HTML", () => {
      const result = htmlToBbcode('<a href="javascript:alert(1)">click</a>');
      expect(result).toBe("click");
      expect(result).not.toContain("[link");
    });

    it("sanitizes data: links from external HTML", () => {
      const result = htmlToBbcode(
        '<a href="data:text/html,<script>alert(1)</script>">xss</a>',
      );
      expect(result).toBe("xss");
      expect(result).not.toContain("[link");
    });

    it("preserves safe links from external HTML with their text", () => {
      const result = htmlToBbcode('<a href="https://safe.com">safe link</a>');
      expect(result).toBe('[link text="safe link"]https://safe.com[/link]');
    });
  });

  describe("unmarked image XSS prevention", () => {
    it("removes javascript: images from external HTML", () => {
      const result = htmlToBbcode('<img src="javascript:alert(1)" />');
      expect(result).toBe("");
      expect(result).not.toContain("[img");
    });

    it("removes data: images from external HTML", () => {
      const result = htmlToBbcode(
        '<img src="data:image/svg+xml,<svg onload=alert(1)/>" />',
      );
      expect(result).toBe("");
      expect(result).not.toContain("[img");
    });

    it("preserves safe images from external HTML", () => {
      const result = htmlToBbcode(
        '<img src="https://example.com/image.png" />',
      );
      expect(result).toBe("[img]https://example.com/image.png[/img]");
    });
  });

  describe("marked link XSS prevention", () => {
    it("sanitizes javascript: in marked links", () => {
      const result = htmlToBbcode(
        '<a href="javascript:alert(1)" data-bb-tag="link">text</a>',
      );
      expect(result).toBe("text");
      expect(result).not.toContain("[link");
    });

    it("sanitizes data: in marked links", () => {
      const result = htmlToBbcode(
        '<a href="data:text/html,evil" data-bb-tag="link">text</a>',
      );
      expect(result).toBe("text");
      expect(result).not.toContain("[link");
    });
  });

  describe("marked image XSS prevention", () => {
    it("removes javascript: from marked images", () => {
      const result = htmlToBbcode(
        '<img src="javascript:alert(1)" data-bb-tag="img" />',
      );
      expect(result).toBe("");
    });

    it("removes data: from marked images", () => {
      const result = htmlToBbcode(
        '<img src="data:image/png;base64,evil" data-bb-tag="img" />',
      );
      expect(result).toBe("");
    });
  });
});

describe("XSS Prevention - Round Trip Safety", () => {
  // The author's text comes back as it was: the server refuses the address
  // again when it renders the post, and nothing in between ever made it an
  // element with that address.
  it("dangerous links come back as the text the author typed", () => {
    const malicious = '[link text="click"]javascript:alert(1)[/link]';
    const html = bbcodeToHtml(malicious);
    expect(html).not.toMatch(/href="javascript/);
    expect(htmlToBbcode(html)).toBe(malicious);
  });

  it("dangerous images come back as the text the author typed", () => {
    const malicious = "[img]javascript:alert(1)[/img]";
    const html = bbcodeToHtml(malicious);
    expect(html).not.toMatch(/src="javascript/);
    expect(htmlToBbcode(html)).toBe(malicious);
  });

  it("safe URLs survive round-trip", () => {
    const safe = '[link text="safe link"]https://example.com[/link]';
    const html = bbcodeToHtml(safe);
    const result = htmlToBbcode(html);
    expect(result).toBe(safe);
  });

  it("safe images survive round-trip", () => {
    const safe = "[img]https://example.com/photo.jpg[/img]";
    const html = bbcodeToHtml(safe);
    const result = htmlToBbcode(html);
    expect(result).toBe(safe);
  });
});

describe("addressRefusal: what an address field says", () => {
  const SCHEME = "Адрес начинается с http:// или https://";
  const MALFORMED = "Адрес записан с ошибкой";
  const LOCAL = "Адрес ведет в локальную сеть, такой сайт не покажет";

  it.each([
    ["", null],
    ["https://example.com/a.png", null],
    [" https://example.com/a.png ", null],
    ["example.com", SCHEME],
    ["javascript:alert(1)", SCHEME],
    ["/images/a.gif", SCHEME],
    ["https://", MALFORMED],
    ["https://example.com/a b.png", MALFORMED],
    // A space the server reads as one, and a browser's URL would take.
    ["https://example.com/a\u2009b.png", MALFORMED],
    ["https://exa%41mple.com/", MALFORMED],
    ["http://localhost/", LOCAL],
    ["http://192.168.0.1/a.png", LOCAL],
  ])("%j: %j", (value, refusal) => {
    expect(addressRefusal(value)).toBe(refusal);
  });

  it("says nothing where the server shows the address", () => {
    for (const value of ["https://example.com/", "http://example.com/a?b=c"]) {
      expect(sanitizeUrl(value)).not.toBe("#");
      expect(addressRefusal(value)).toBeNull();
    }
  });
});
