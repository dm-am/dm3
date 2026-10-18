/**
 * @vitest-environment jsdom
 */

/**
 * The journal names each entry for what the server wrote it for.
 *
 * The one gated here is the address change, because its name and its moment
 * came apart. The server writes `EmailChange` when a new address is ASKED for
 * (EmailChangeService.Change, beside the warning letter to the old address);
 * the step that actually moves the address, Confirm, writes no entry at all.
 * The journal called that entry "Изменение почты", claiming a move that had
 * not happened - on the screen a person opens to learn what somebody else may
 * have done to the account, where a request that can still be stopped by a new
 * password and a move already made call for different acts.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import AccountSecurityHistorySection from "./AccountSecurityHistorySection.vue";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { compileSass, sassBlocks, ruleMap } from "@/shared/lib/testing/styles";
import type { SecurityEvent } from "@/shared/api/models/account";

const event = (over: Partial<SecurityEvent> = {}): SecurityEvent => ({
  id: "event-1",
  eventType: "PasswordChange",
  timestampUtc: "2026-09-20T10:00:00Z",
  ...over,
});

async function render(events: SecurityEvent[]) {
  vi.spyOn(accountApi, "getSecurityHistory").mockResolvedValue({
    data: { resources: events },
    error: null,
  } as never);

  const wrapper = mount(AccountSecurityHistorySection, {
    global: { plugins: [createPinia()] },
  });
  await flushPromises();
  return wrapper;
}

const titles = (wrapper: Awaited<ReturnType<typeof render>>) =>
  wrapper.findAll(".event-title").map((title) => title.text());

describe("the security journal of the account page", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.restoreAllMocks();
  });

  it("names the address entry as the request it is, not as a move", async () => {
    const wrapper = await render([event({ eventType: "EmailChange" })]);

    expect(titles(wrapper)).toEqual(["Запрос смены почты"]);
    expect(wrapper.text()).not.toContain("Изменение почты");
  });

  // The password entry IS written on the change itself
  // (PasswordChangeService), so its name keeps the finished act.
  it("keeps the finished act on the entry the server writes on the act", async () => {
    const wrapper = await render([event({ eventType: "PasswordChange" })]);

    expect(titles(wrapper)).toEqual(["Изменение пароля"]);
  });
});

// The owner's rule of 5 October 2026: no row of the site scrolls sideways or
// breaks in two. The line under an entry was device, address and date with
// bullets between them, scrolled sideways on a phone with the date out of
// sight. The date went up into the heading, and what is left is a caption.
describe("an entry of the security journal", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.restoreAllMocks();
  });

  const signedIn = event({
    eventType: "LoginSuccess",
    deviceInfo: "Chrome на Windows",
    ipAddress: "172.28.0.1",
  });

  it("says what happened and when in its heading", async () => {
    const wrapper = await render([signedIn]);

    expect(wrapper.get(".event-heading").text()).toMatch(
      /^Успешный вход, \d{2}\.\d{2}\.\d{4}\sв\s\d{2}:\d{2}$/,
    );
  });

  it("says from where under it, with the bar of a caption and no date", async () => {
    const wrapper = await render([signedIn]);

    const line = wrapper.get(".event-details");
    expect(line.classes()).toContain("caption-line");
    expect(line.text()).toBe("Chrome на Windows | 172.28.0.1");
    expect(wrapper.find(".scroll-row").exists()).toBe(false);
  });

  it("draws no second line when the server knows neither", async () => {
    const wrapper = await render([event()]);

    expect(wrapper.find(".event-details").exists()).toBe(false);
    expect(wrapper.get(".event-heading").text()).toMatch(
      /^Изменение пароля, \d{2}\.\d{2}\.\d{4}/,
    );
  });
});

describe("the date in the heading of an entry", () => {
  // A long name breaks between its words, as a heading does; the date after
  // it never breaks, so "05.10.2026 в" is never left at the end of a line
  // with the time under it.
  it("is never broken in two", () => {
    const file = join(
      dirname(fileURLToPath(import.meta.url)),
      "AccountSecurityHistorySection.vue",
    );
    const rules = ruleMap(compileSass(sassBlocks(file), file));
    expect(rules.get(".event-date")?.["white-space"]).toBe("nowrap");
  });
});
