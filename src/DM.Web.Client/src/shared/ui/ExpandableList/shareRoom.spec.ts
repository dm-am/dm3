/**
 * @vitest-environment node
 */

/**
 * How the flexible columns of a list share the room of a row. A table laid
 * out "fixed" shares it equally, and on a phone the staff table broke
 * "Администраторы" in the middle next to a column of short logins: the list
 * gives every column its longest word first, and a column whose lines wrap
 * is the one that gives way.
 */
import { describe, expect, it } from "vitest";
import { shareRoom } from "./shareRoom";

describe("shareRoom", () => {
  it("gives every column its longest word and shares the rest equally", () => {
    // 600 of room, words of 120, 100 and 180: 200 left, a third each.
    const widths = shareRoom(600, [
      { want: 120, hold: 120 },
      { want: 100, hold: 100 },
      { want: 180, hold: 50 },
    ]);

    expect(widths.map(Math.round)).toEqual([187, 167, 247]);
  });

  it("takes the room a short row lacks from the column that gives way", () => {
    // The role at "Администраторы", the logins at what is left: 235 - 129.
    const widths = shareRoom(235, [
      { want: 129, hold: 129 },
      { want: 188, hold: 55 },
    ]);

    expect(widths).toEqual([129, 106]);
  });

  it("never takes a column that gives way below its label", () => {
    const widths = shareRoom(150, [
      { want: 129, hold: 129 },
      { want: 188, hold: 55 },
    ]);

    // Both at what they hold: the list is wider than its box.
    expect(widths).toEqual([129, 55]);
  });

  it("takes from several columns that give way evenly, none below its label", () => {
    // 100 over: 50 from each, but the second can give only 30, so the first
    // gives the remaining 20 too.
    const widths = shareRoom(300, [
      { want: 100, hold: 100 },
      { want: 150, hold: 50 },
      { want: 150, hold: 120 },
    ]);

    expect(widths).toEqual([100, 80, 120]);
  });

  it("leaves a column that does not give way at its longest word when the row is short", () => {
    const widths = shareRoom(200, [
      { want: 150, hold: 150 },
      { want: 150, hold: 150 },
    ]);

    expect(widths).toEqual([150, 150]);
  });
});
