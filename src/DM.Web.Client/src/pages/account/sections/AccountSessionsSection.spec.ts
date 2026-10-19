/**
 * @vitest-environment jsdom
 */

/**
 * The sessions block: "Выйти со всех других устройств" answers beside the
 * button, the list untouched, so the sessions it failed to end still offer
 * "Завершить". A session is a framed row: the device, under it a caption line
 * of the address and the date, and "Завершить" beside them.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import AccountSessionsSection from "./AccountSessionsSection.vue";
import type { SessionInfo } from "@/shared/api/models/account";

const options = () => ({ global: { plugins: [createPinia()] } });

const session = (id: string, isCurrent: boolean): SessionInfo => ({
  id,
  isCurrent,
  persistent: true,
  expirationUtc: "2027-05-01T10:00:00Z",
  createdUtc: "2026-05-01T10:00:00Z",
  deviceInfo: "Chrome, Windows",
  ipAddress: "10.0.0.1",
});

/** Mounts the block over one open session of this device and one of another. */
async function render() {
  vi.spyOn(accountApi, "getSessions").mockResolvedValue({
    data: { resources: [session("here", true), session("elsewhere", false)] },
    error: null,
  } as never);

  const wrapper = mount(AccountSessionsSection, options());
  await flushPromises();
  return wrapper;
}

/** The control that ends every session but this one. */
const logoutAll = (wrapper: VueWrapper) =>
  wrapper.get(".sessions-actions button");

describe("the sessions block of the account page", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.restoreAllMocks();
  });

  // The line says the refusal and the plate is taken down (refusalHere).
  it("says a 500 beside the button", async () => {
    const wrapper = await render();
    vi.spyOn(accountApi, "logoutAll").mockResolvedValue({
      data: null,
      error: { status: 500, title: "Ошибка сервера" },
    } as never);

    await logoutAll(wrapper).trigger("click");
    await flushPromises();

    expect(wrapper.get(".sessions-refusal").text()).toBe("Ошибка сервера");
    // The other session is still listed and still offers its press.
    expect(wrapper.findAll(".session-item")).toHaveLength(2);
    expect(wrapper.find(".terminate-btn").exists()).toBe(true);
  });

  it("reads out a refusal the interceptor keeps quiet about", async () => {
    const wrapper = await render();
    vi.spyOn(accountApi, "logoutAll").mockResolvedValue({
      data: null,
      error: { status: 409, title: "Сессии уже завершены" },
    } as never);

    await logoutAll(wrapper).trigger("click");
    await flushPromises();

    expect(wrapper.get(".sessions-refusal").text()).toBe(
      "Сессии уже завершены",
    );
    expect(wrapper.findAll(".session-item")).toHaveLength(2);
  });

  it("leaves only this device's session when the press goes through", async () => {
    const wrapper = await render();
    vi.spyOn(accountApi, "logoutAll").mockResolvedValue({
      data: null,
      error: null,
    } as never);

    await logoutAll(wrapper).trigger("click");
    await flushPromises();

    expect(wrapper.findAll(".session-item")).toHaveLength(1);
    expect(wrapper.find(".sessions-refusal").exists()).toBe(false);
  });

  // The bar of the caption line stands a space away from either part.
  it("sets the address and the date apart with the caption line's bar", async () => {
    const wrapper = await render();
    const details = wrapper.findAll(".session-details");

    expect(details).toHaveLength(2);
    for (const line of details) {
      expect(line.classes()).toContain("caption-line");
      expect(line.findAll(".caption-line__gap")).toHaveLength(1);
      expect(line.text()).toMatch(/^10\.0\.0\.1 \| \d{2}\.\d{2}\.\d{4}/);
    }
  });

  // A long IPv6 address of a mobile network is a part of its own, written whole.
  it("writes a long address whole, as a part of its own", async () => {
    const mobileIp = "2a00:1fa0:4612:9b47:1d3c:8e2f:5a10:7c2b";
    vi.spyOn(accountApi, "getSessions").mockResolvedValue({
      data: {
        resources: [
          session("here", true),
          { ...session("elsewhere", false), ipAddress: mobileIp },
        ],
      },
      error: null,
    } as never);
    const wrapper = mount(AccountSessionsSection, options());
    await flushPromises();

    const line = wrapper.findAll(".session-details")[1];
    const [address, date] = line.findAll(".caption-line__part");
    expect(address.text()).toBe(mobileIp);
    expect(date.text()).toMatch(/^\d{2}\.\d{2}\.\d{4} в\s\d{2}:\d{2}$/);
    expect(line.text()).not.toContain("…");
    expect(line.text()).not.toContain("Еще");
  });

  it("draws no bar where the address is unknown", async () => {
    vi.spyOn(accountApi, "getSessions").mockResolvedValue({
      data: { resources: [{ ...session("here", true), ipAddress: undefined }] },
      error: null,
    } as never);
    const wrapper = mount(AccountSessionsSection, options());
    await flushPromises();

    const line = wrapper.get(".session-details");
    expect(line.find(".caption-line__gap").exists()).toBe(false);
    expect(line.text()).toMatch(/^\d{2}\.\d{2}\.\d{4}/);
  });

  // The session of this device stands apart under a caption, not in a pill.
  describe("this device's session", () => {
    it("stands first, under its own caption, and the rest under theirs", async () => {
      const wrapper = await render();

      const groups = wrapper.findAll(".session-group");
      expect(groups.map((g) => g.get(".session-group-title").text())).toEqual([
        "Текущая сессия",
        "Другие сессии",
      ]);
      expect(groups[0].findAll(".session-item")).toHaveLength(1);
      expect(groups[0].find(".terminate-btn").exists()).toBe(false);
      expect(groups[1].findAll(".session-item")).toHaveLength(1);
      expect(groups[1].find(".terminate-btn").exists()).toBe(true);
    });

    it("draws no group that has nobody in it", async () => {
      vi.spyOn(accountApi, "getSessions").mockResolvedValue({
        data: { resources: [session("here", true)] },
        error: null,
      } as never);
      const wrapper = mount(AccountSessionsSection, options());
      await flushPromises();

      expect(
        wrapper.findAll(".session-group-title").map((t) => t.text()),
      ).toEqual(["Текущая сессия"]);
    });

    it("wears no pill, no word and no mark of its own", async () => {
      const wrapper = await render();

      expect(wrapper.get(".session-device").text()).toBe("Chrome, Windows");
      const [here, elsewhere] = wrapper.findAll(".session-item");
      expect(here.classes()).toEqual(elsewhere.classes());
    });
  });
});
