/**
 * @vitest-environment jsdom
 */

/**
 * The journal names each entry for what the server wrote it for: the address
 * change is written when a new address is asked for, so it is a request.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import AccountSecurityHistorySection from "./AccountSecurityHistorySection.vue";
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
  });

  // The password entry is written on the change itself, so it keeps the finished act.
  it("keeps the finished act on the entry the server writes on the act", async () => {
    const wrapper = await render([event({ eventType: "PasswordChange" })]);

    expect(titles(wrapper)).toEqual(["Изменение пароля"]);
  });
});

// The date is in the heading, and what is under it is a caption line.
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
  });

  it("draws no second line when the server knows neither", async () => {
    const wrapper = await render([event()]);

    expect(wrapper.find(".event-details").exists()).toBe(false);
    expect(wrapper.get(".event-heading").text()).toMatch(
      /^Изменение пароля, \d{2}\.\d{2}\.\d{4}/,
    );
  });
});
