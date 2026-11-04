/**
 * @vitest-environment jsdom
 */

/**
 * The empty edges of a text go before it is drawn; empty lines inside stay,
 * and a block holding a picture is not empty.
 */

import { describe, it, expect } from "vitest";
import { trimHtmlWhitespace } from "./trimHtmlWhitespace";

describe("trimHtmlWhitespace", () => {
  it("takes the breaks, empty blocks and blanks off both edges", () => {
    expect(
      trimHtmlWhitespace("<br> <p></p><div> </div>  слово<br><p></p> <br>"),
    ).toBe("слово");
  });

  it("goes into the first and the last block for their edges", () => {
    expect(trimHtmlWhitespace("<p><br> раз</p><p>два <br></p>")).toBe(
      "<p>раз</p><p>два</p>",
    );
  });

  it("keeps the empty lines inside the text", () => {
    expect(trimHtmlWhitespace("раз<br><br><p></p>два")).toBe(
      "раз<br><br><p></p>два",
    );
  });

  it("keeps a block that looks empty but holds a picture", () => {
    const picture = '<p><img src="https://example.com/a.png"></p>';
    expect(trimHtmlWhitespace(`<br>${picture}<br>`)).toBe(picture);
  });

  it("gives an empty string for no text", () => {
    expect(trimHtmlWhitespace(null)).toBe("");
    expect(trimHtmlWhitespace(undefined)).toBe("");
    expect(trimHtmlWhitespace("")).toBe("");
    expect(trimHtmlWhitespace("<br><p> </p>")).toBe("");
  });
});
