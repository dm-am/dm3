/**
 * The virtualized chat rows.
 *
 * Both chat pages used to read the mixed list at the virtualizer's index
 * inside every template binding and cast the result, so nothing ever checked
 * that the member being read belonged to the kind of item actually there. The
 * mapping below is where the two kinds are told apart now, which makes it the
 * one place worth pinning: the narrowed value each row carries, and what
 * happens to a row whose item is gone.
 */
import { describe, it, expect } from "vitest";
import {
  openImageGates,
  toChatVirtualRows,
  type MessageOrSeparator,
  type VirtualRowGeometry,
} from "./chat";

const separator = {
  type: "date-separator",
  date: "2026-08-11",
  formattedDate: "Сегодня",
} as MessageOrSeparator;

const message = (id: string) =>
  ({
    id,
    createdUtc: "2026-08-11T10:00:00Z",
    modifiedUtc: null,
    author: { username: "Alice" },
    text: "привет",
    isRemoved: false,
    deletedBy: null,
    deletedUtc: null,
    likes: [],
    edits: [],
    isContinuation: false,
  }) as unknown as MessageOrSeparator;

const geometry = (index: number, key: string | number): VirtualRowGeometry => ({
  index,
  key,
  start: index * 80,
});

describe("toChatVirtualRows", () => {
  it("carries the narrowed item and the geometry of each visible row", () => {
    const items = [separator, message("m1"), message("m2")];
    const rows = toChatVirtualRows(items, [
      geometry(0, "sep-2026-08-11"),
      geometry(2, "m2"),
    ]);

    expect(rows).toHaveLength(2);
    expect(rows[0]).toMatchObject({
      kind: "separator",
      key: "sep-2026-08-11",
      index: 0,
      start: 0,
    });
    expect(
      rows[0].kind === "separator" && rows[0].separator.formattedDate,
    ).toBe("Сегодня");
    expect(rows[1]).toMatchObject({ kind: "message", key: "m2", index: 2 });
    expect(rows[1].kind === "message" && rows[1].message.id).toBe("m2");
  });

  it("stringifies the virtualizer's own index key when no key was supplied", () => {
    const rows = toChatVirtualRows([message("m1")], [geometry(0, 0)]);

    expect(rows[0].key).toBe("0");
  });

  it("drops a row the list no longer has an item for", () => {
    // The window can outlive the list it was measured against — a jump to
    // another archive date replaces the messages under it with fewer.
    const rows = toChatVirtualRows(
      [message("m1")],
      [geometry(0, "m1"), geometry(7, "gone")],
    );

    expect(rows).toHaveLength(1);
    expect(rows[0].index).toBe(0);
  });
});

/**
 * An image of the global chat: behind the server's spoiler in the compact
 * layout, in plain view in the full one. The spoiler an author wrote is theirs
 * and stays as it is.
 */
describe("openImageGates", () => {
  const image =
    '<img src="https://example.com/a.png" alt="" class="bb-image" data-bb-tag="img" />';
  const gated =
    '<a href="#" class="spoiler-head" data-bb-gate="image" data-swaptext="Скрыть содержимое">Показать содержимое</a>' +
    `<div class="spoiler" data-bb-gate="image">${image}</div>`;

  it("shows the image where it stands", () => {
    const template = document.createElement("template");
    template.innerHTML = openImageGates(`раз<br />${gated}<br />два`);
    const content = template.content;
    expect(content.querySelector(".spoiler-head")).toBeNull();
    expect(content.querySelector(".spoiler")).toBeNull();
    expect(content.querySelector("img")?.getAttribute("src")).toBe(
      "https://example.com/a.png",
    );
    expect(content.textContent).toBe("раздва");
  });

  it("leaves a spoiler the author wrote as it is", () => {
    const authored =
      '<a href="#" class="spoiler-head">Показать содержимое</a>' +
      `<div class="spoiler">текст ${gated}</div>`;
    const template = document.createElement("template");
    template.innerHTML = openImageGates(authored);
    const heads = template.content.querySelectorAll(".spoiler-head");
    expect(heads).toHaveLength(1);
    expect(heads[0].hasAttribute("data-bb-gate")).toBe(false);
    expect(
      template.content.querySelector(".spoiler > img")?.getAttribute("src"),
    ).toBe("https://example.com/a.png");
  });

  it("leaves a message without a gated image untouched", () => {
    const html =
      '<a href="#" class="spoiler-head">x</a><div class="spoiler">y</div>';
    expect(openImageGates(html)).toBe(html);
  });
});
