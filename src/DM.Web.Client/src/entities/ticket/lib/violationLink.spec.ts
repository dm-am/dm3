/**
 * @vitest-environment jsdom
 */

/**
 * The address a complaint carries is a link where it can be one, and text
 * everywhere else.
 *
 * The server writes the address of the violation into the text of the ticket
 * as a line of words ("Ссылка на нарушение: /games/example"), rendered with no
 * tag for a bare address, so every reader saw it as plain text. The line is a
 * link now when its address is a path of this site, a full address of the
 * page's own origin, or an http or https address a [link] of a text would take;
 * "javascript:", another host disguised as a path, the reader's own machine
 * and anything that is not an address stay the words they were.
 */
import { describe, it, expect } from "vitest";
import { linkTicketAddress, ticketLinkHref } from "./violationLink";

/** The server's rendering of a complaint: the text, a blank line, the link line. */
const rendered = (line: string, after = "") =>
  `Оскорбляет участников.<br /><br />${line}${after}`;

/** The links of a rendering, read back as a reader's browser would. */
function links(html: string) {
  const host = document.createElement("div");
  host.innerHTML = html;
  return Array.from(host.querySelectorAll("a")).map((a) => ({
    href: a.getAttribute("href"),
    text: a.textContent,
    target: a.getAttribute("target"),
    rel: a.getAttribute("rel"),
  }));
}

const origin = window.location.origin;

describe("where the address of a ticket leads", () => {
  it.each([
    ["/games/example", "/games/example"],
    ["/users/LongestLoginPossible", "/users/LongestLoginPossible"],
    ["/forum/general/5?page=2#comment-1", "/forum/general/5?page=2#comment-1"],
    // A full address of the site the reader is on is the path, so it works on
    // whichever address of the site the reader came by.
    [`${origin}/games/example`, "/games/example"],
    ["https://example.com/topic/1", "https://example.com/topic/1"],
    ["http://example.com/", "http://example.com/"],
  ])("%s leads to %s", (address, href) => {
    expect(ticketLinkHref(address)).toBe(href);
  });

  it.each([
    "javascript:alert(1)",
    "JavaScript:alert(1)",
    "data:text/html,<b>x</b>",
    // A browser reads both as another host.
    "//evil.example/x",
    "/\\evil.example/x",
    // The reader's own machine, refused as a [link] of a text refuses it.
    "http://127.0.0.1:8080/admin",
    "http://localhost:9000/",
    // Words, not an address: resolved against the page they would have led
    // somewhere all the same.
    "games/example",
    "example.com",
    "ftp://example.com/file",
  ])("%s stays text", (address) => {
    expect(ticketLinkHref(address)).toBeNull();
  });
});

describe("the link line of a ticket", () => {
  it("makes a path of the site a link, its words kept as they were", () => {
    const html = linkTicketAddress(
      rendered("Ссылка на нарушение: /games/example"),
    );

    expect(links(html)).toEqual([
      {
        href: "/games/example",
        text: "/games/example",
        target: "_blank",
        rel: "noopener noreferrer",
      },
    ]);
    const host = document.createElement("div");
    host.innerHTML = html;
    expect(host.textContent).toBe(
      "Оскорбляет участников.Ссылка на нарушение: /games/example",
    );
  });

  it("makes an outside address a link that hands the page nothing", () => {
    const html = linkTicketAddress(
      rendered("Ссылка на решение: https://example.com/a?b=1&amp;c=2"),
    );

    expect(links(html)).toEqual([
      {
        href: "https://example.com/a?b=1&c=2",
        text: "https://example.com/a?b=1&c=2",
        target: "_blank",
        rel: "noopener noreferrer",
      },
    ]);
  });

  it("finds the line with the contact written after it", () => {
    const html = linkTicketAddress(
      rendered(
        "Ссылка на нарушение: /users/Name",
        "<br /><br />Контакт для ответа: me@example.com",
      ),
    );

    expect(links(html).map((link) => link.href)).toEqual(["/users/Name"]);
  });

  it.each([
    "Ссылка на нарушение: javascript:alert(1)",
    "Ссылка на нарушение: //evil.example/x",
    "Ссылка на нарушение: где-то в комментариях",
  ])("leaves %s as text", (line) => {
    const html = rendered(line);

    expect(linkTicketAddress(html)).toBe(html);
  });

  it("leaves the caption alone where it is not a line of its own", () => {
    const html =
      "Смотри <strong>тут</strong> Ссылка на нарушение: /games/example";

    expect(linkTicketAddress(html)).toBe(html);
  });

  it("hands a text without the line back untouched", () => {
    const html = "Не открывается <strong>страница</strong>";

    expect(linkTicketAddress(html)).toBe(html);
  });
});
