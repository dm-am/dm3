/**
 * Reserving the box of an attachment picture placed in the post's text.
 *
 * The avatar has declared its width and height for as long as it has existed,
 * and that pair is the whole anti-shift mechanism: the browser reads a ratio
 * off it and lays the text out once, instead of pushing it down when the
 * picture decodes. A picture in the body of a post had no such pair — the
 * BBCode renderer emits none, because for an arbitrary address there is nothing
 * to put there. For the post's own attachments there is: the upload pipeline
 * measured them and the payload carries the numbers.
 *
 * So what these assert is the boundary of "we know the size": the post's own
 * attachments get the pair, and everything else — somebody else's upload, an
 * external address, an attachment stored before the pipeline measured — is left
 * exactly as it came, because a guessed box is a shift with extra steps.
 */
import { describe, expect, it } from "vitest";
import type { PostAttachment } from "@/entities/game";
import { reserveAttachmentImageBoxes } from "./postAttachments";

const MAP_ID = "571c0beb-1890-9ba6-2170-4bb531bc51f6";

/**
 * An absolute address, because that is the only spelling that survives the
 * BBCode sanitizer — it passes http and https and nothing else, so a picture in
 * the text always carries a host. Which host is deliberately not this site's:
 * the identifier in the path is what names the upload, and a rule keyed on the
 * origin would break the day the site answers on a second one.
 */
const ORIGIN = "https://example.test";

function attachment(over: Partial<PostAttachment> = {}): PostAttachment {
  return {
    id: MAP_ID,
    fileName: "карта-подземелья.jpg",
    contentType: "image/jpeg",
    sizeBytes: 6707,
    width: 125,
    height: 138,
    url: `/v1/uploads/${MAP_ID}/content`,
    createdUtc: "2026-01-01T00:00:00Z",
    ...over,
  };
}

function image(src: string, extra = ""): string {
  return `<p>перед<img src="${src}" class="bb-image" data-bb-tag="img" loading="lazy"${extra} />после</p>`;
}

/** width/height of the single <img> in the result, or null when undeclared. */
function declared(html: string): [string, string] | null {
  const template = document.createElement("template");
  template.innerHTML = html;
  const img = template.content.querySelector("img");
  const width = img?.getAttribute("width");
  const height = img?.getAttribute("height");
  return width && height ? [width, height] : null;
}

describe("reserveAttachmentImageBoxes", () => {
  it("declares the measured box for the post's own attachment", () => {
    const html = image(`${ORIGIN}/v1/uploads/${MAP_ID}/content`);
    expect(declared(reserveAttachmentImageBoxes(html, [attachment()]))).toEqual(
      ["125", "138"],
    );
  });

  it("matches the path spelling the payload itself uses", () => {
    const html = image(`/v1/uploads/${MAP_ID}/content`);
    expect(declared(reserveAttachmentImageBoxes(html, [attachment()]))).toEqual(
      ["125", "138"],
    );
  });

  it("matches whatever case the identifier was typed in", () => {
    const html = image(`${ORIGIN}/v1/uploads/${MAP_ID.toUpperCase()}/content`);
    expect(declared(reserveAttachmentImageBoxes(html, [attachment()]))).toEqual(
      ["125", "138"],
    );
  });

  it("matches an address carrying a query or a fragment", () => {
    for (const suffix of ["?v=2", "#top"]) {
      const html = image(`${ORIGIN}/v1/uploads/${MAP_ID}/content${suffix}`);
      expect(
        declared(reserveAttachmentImageBoxes(html, [attachment()])),
      ).toEqual(["125", "138"]);
    }
  });

  it("leaves an external picture alone", () => {
    const html = image("https://i.ibb.co/BZqn22s/XII-III.png");
    const out = reserveAttachmentImageBoxes(html, [attachment()]);
    expect(out).toBe(html);
    expect(declared(out)).toBeNull();
  });

  it("leaves an upload that is not this post's attachment alone", () => {
    const other = "00000000-1111-2222-3333-444444444444";
    const html = image(`${ORIGIN}/v1/uploads/${other}/content`);
    expect(reserveAttachmentImageBoxes(html, [attachment()])).toBe(html);
  });

  it("leaves an attachment stored before the pipeline measured it", () => {
    const html = image(`/v1/uploads/${MAP_ID}/content`);
    const unmeasured = [attachment({ width: null, height: null })];
    expect(reserveAttachmentImageBoxes(html, unmeasured)).toBe(html);
  });

  it("leaves a pair that is not a usable ratio", () => {
    const html = image(`/v1/uploads/${MAP_ID}/content`);
    for (const broken of [
      attachment({ width: 0, height: 138 }),
      attachment({ width: 125, height: 0 }),
      attachment({ width: -125, height: 138 }),
      attachment({ height: undefined }),
    ]) {
      expect(reserveAttachmentImageBoxes(html, [broken])).toBe(html);
    }
  });

  it("does not overwrite a box the markup already declares", () => {
    const html = image(
      `/v1/uploads/${MAP_ID}/content`,
      ' width="40" height="40"',
    );
    const out = reserveAttachmentImageBoxes(html, [attachment()]);
    expect(out).toBe(html);
    expect(declared(out)).toEqual(["40", "40"]);
  });

  it("sizes every attachment picture in a post that carries several", () => {
    const second = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
    const html =
      image(`/v1/uploads/${MAP_ID}/content`) +
      image(`/v1/uploads/${second}/content`);
    const out = reserveAttachmentImageBoxes(html, [
      attachment(),
      attachment({ id: second, width: 800, height: 200 }),
    ]);
    const template = document.createElement("template");
    template.innerHTML = out;
    expect(
      [...template.content.querySelectorAll("img")].map((i) => [
        i.getAttribute("width"),
        i.getAttribute("height"),
      ]),
    ).toEqual([
      ["125", "138"],
      ["800", "200"],
    ]);
  });

  /**
   * The box is drawn no taller than the stylesheet's cap, and that cap clamps
   * the height without touching a declared width — a pair taller than it would
   * be drawn squashed. The same ratio, shrunk to fit under the cap, reserves
   * exactly the box the picture ends up in.
   */
  it("declares a pair the drawn-height cap cannot squash", () => {
    const html = image(`/v1/uploads/${MAP_ID}/content`);
    const tall = [attachment({ width: 731, height: 793 })];

    expect(declared(reserveAttachmentImageBoxes(html, tall))).toEqual([
      "461",
      "500",
    ]);
  });

  it("keeps the file's own pair when it is drawn whole", () => {
    const html = image(`/v1/uploads/${MAP_ID}/content`);
    const short = [attachment({ width: 800, height: 500 })];

    expect(declared(reserveAttachmentImageBoxes(html, short))).toEqual([
      "800",
      "500",
    ]);
  });

  /**
   * A picture the author gave a size of their own is wrapped in a frame that
   * carries the caps it is drawn to. A second declaration of its box would
   * fight that one, so the frame is left to decide.
   */
  it("leaves a picture the author sized to its frame", () => {
    const html =
      '<span class="bb-image-frame" data-bb-width="300" ' +
      'style="--bb-image-max-width:300px">' +
      `<img src="/v1/uploads/${MAP_ID}/content" class="bb-image" data-bb-tag="img" />` +
      "</span>";

    expect(reserveAttachmentImageBoxes(html, [attachment()])).toBe(html);
  });

  it("hands back text with no picture in it untouched", () => {
    const plain = "<p>Ничего не приложено.</p>";
    expect(reserveAttachmentImageBoxes(plain, [attachment()])).toBe(plain);
    expect(reserveAttachmentImageBoxes(plain, [])).toBe(plain);
    expect(reserveAttachmentImageBoxes(plain, undefined)).toBe(plain);
    expect(reserveAttachmentImageBoxes("", [attachment()])).toBe("");
  });
});
