/**
 * @vitest-environment jsdom
 */

/**
 * The clock the site shows time by. The owner's word on it: a signed-in reader
 * sees every date and time in the zone of the account, a guest in the zone of
 * the device. An account with no zone is UTC, as the letters read it.
 *
 * The device here is in Moscow, set before anything reads the clock, so the
 * guest's answers below are Moscow's and not the machine's the suite runs on.
 * The machine's own zone comes back after the file, for the files that follow
 * in the same worker.
 */
const MACHINE_ZONE = process.env.TZ;
process.env.TZ = "Europe/Moscow";

import { afterAll, afterEach, describe, expect, it } from "vitest";
import { nextTick } from "vue";
import { mount } from "@vue/test-utils";
import {
  DATE_TIME_FORMAT,
  DAY_MONTH_TIME_FORMAT,
  formatDate,
  formatDateFull,
  formatSiteTime,
  fromSiteInput,
  siteTime,
  toSiteInput,
} from "./datetime";
import { formatChatTime, groupMessagesWithSeparators } from "./chat";
import { setAccountTimeZone } from "./timeZone";
import { HumanDate } from "@/shared/ui/Date";
import type { Message } from "@/shared/api/models/common/message";

/** One post of a game room, written at 20:09 UTC on 26 September. */
const POST_CREATED = "2026-09-26T20:09:00Z";

/** U+00A0, the space that does not break a line. */
const NBSP = "\u00a0";

afterEach(() => setAccountTimeZone(null));

afterAll(() => {
  if (MACHINE_ZONE === undefined) delete process.env.TZ;
  else process.env.TZ = MACHINE_ZONE;
});

describe("the time of a post", () => {
  it("is on the device's clock for a guest in Moscow", () => {
    setAccountTimeZone(null);

    expect(formatDateFull(POST_CREATED)).toBe(`26.09.2026 в${NBSP}23:09`);
  });

  it("is on the account's clock for a reader whose account is in Tokyo", () => {
    setAccountTimeZone("Asia/Tokyo");

    // The same moment, on the next day in Tokyo, whatever the device says.
    expect(formatDateFull(POST_CREATED)).toBe(`27.09.2026 в${NBSP}05:09`);
    expect(formatDate(POST_CREATED)).toBe("27.09.2026");
  });

  it("is UTC for an account that names no zone, as the letters read it", () => {
    setAccountTimeZone("");

    expect(formatDateFull(POST_CREATED)).toBe(`26.09.2026 в${NBSP}20:09`);
  });

  // In a narrow cell the line broke after the preposition: "01.01.2020 в" on
  // one line and "03:00" alone on the next. The space after "в" does not
  // break, so the line breaks before it and the time keeps its "в". The
  // space before "в" is an ordinary one: that is where the break goes.
  it.each([
    ["with the year", DATE_TIME_FORMAT],
    ["without the year", DAY_MONTH_TIME_FORMAT],
  ])("keeps the time with its preposition, %s", (_, format) => {
    expect(formatSiteTime(POST_CREATED, format)).toMatch(
      new RegExp(`^[\\d.]+ в${NBSP}\\d\\d:\\d\\d$`),
    );
  });

  it("is drawn again when the zone of the account arrives", async () => {
    const wrapper = mount(HumanDate, { props: { date: POST_CREATED } });
    expect(wrapper.text()).toBe(`26.09.2026 в${NBSP}23:09`);

    setAccountTimeZone("Asia/Tokyo");
    await nextTick();

    expect(wrapper.text()).toBe(`27.09.2026 в${NBSP}05:09`);
  });
});

describe("the site's clock", () => {
  it("reads a zone on summer time at the offset it had at that moment", () => {
    setAccountTimeZone("America/New_York");

    expect(formatSiteTime("2026-01-15T12:00:00Z", "HH:mm")).toBe("07:00");
    expect(formatSiteTime("2026-07-15T12:00:00Z", "HH:mm")).toBe("08:00");
  });

  it("keeps the moment itself: only the reading of it moves", () => {
    setAccountTimeZone("Asia/Tokyo");

    expect(siteTime(POST_CREATED).valueOf()).toBe(Date.parse(POST_CREATED));
  });

  // A bare day of a filter or of the chat's separators is the same day on
  // every clock. Read as the device's midnight and moved west, it would turn
  // into the day before.
  it("leaves a day of the calendar as the day it is", () => {
    setAccountTimeZone("America/Los_Angeles");

    expect(formatDate("2026-09-27")).toBe("27.09.2026");
  });

  it("falls back to the device for a zone this browser does not know", () => {
    setAccountTimeZone("Mars/Olympus_Mons");

    expect(formatDateFull(POST_CREATED)).toBe(`26.09.2026 в${NBSP}23:09`);
  });
});

// A date-and-time field holds a time of day with no zone, and the reader fills
// it in by the clock the site shows them. Read on the device's clock instead, a
// reader in Tokyo on a device in Moscow typed 20:00 and sent 20:00 Moscow.
describe("a date-and-time field", () => {
  it("sends 20:00 typed by a reader in Tokyo as 20:00 in Tokyo", () => {
    setAccountTimeZone("Asia/Tokyo");

    expect(fromSiteInput("2026-10-01T20:00")).toBe("2026-10-01T11:00:00.000Z");
  });

  it("opens on the account's clock and comes back to the same moment", () => {
    setAccountTimeZone("Asia/Tokyo");

    expect(toSiteInput(POST_CREATED)).toBe("2026-09-27T05:09");
    expect(fromSiteInput(toSiteInput(POST_CREATED))).toBe(
      new Date(POST_CREATED).toISOString(),
    );
  });

  it("is on the device's clock for a guest", () => {
    setAccountTimeZone(null);

    expect(toSiteInput(POST_CREATED)).toBe("2026-09-26T23:09");
    expect(fromSiteInput("2026-10-01T20:00")).toBe("2026-10-01T17:00:00.000Z");
  });

  it("is UTC for an account that names no zone", () => {
    setAccountTimeZone("");

    expect(fromSiteInput("2026-10-01T20:00")).toBe("2026-10-01T20:00:00.000Z");
  });

  // Both sides of the night New York moves its clocks forward: 01:30 is still
  // winter time (UTC-5), 03:30 already summer time (UTC-4).
  it("reads a zone on summer time at the offset of the time typed", () => {
    setAccountTimeZone("America/New_York");

    expect(fromSiteInput("2026-03-08T01:30")).toBe("2026-03-08T06:30:00.000Z");
    expect(fromSiteInput("2026-03-08T03:30")).toBe("2026-03-08T07:30:00.000Z");
  });

  it("has no moment for a cleared field", () => {
    setAccountTimeZone("Asia/Tokyo");

    expect(fromSiteInput("")).toBeNull();
  });
});

describe("the chat", () => {
  const message = (id: string, createdUtc: string) =>
    ({
      id,
      createdUtc,
      text: "",
      author: { username: "reader" },
    }) as unknown as Message;

  it("stamps a message and files it under a day on the account's clock", () => {
    setAccountTimeZone("Asia/Tokyo");

    // 20:09 UTC is five past five the next morning in Tokyo.
    expect(formatChatTime(POST_CREATED)).toBe("05:09");
    const [separator] = groupMessagesWithSeparators([
      message("1", POST_CREATED),
    ]);
    expect(separator).toMatchObject({ date: "2026-09-27" });
  });
});
