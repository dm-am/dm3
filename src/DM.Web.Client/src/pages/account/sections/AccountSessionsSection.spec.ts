/**
 * @vitest-environment jsdom
 */

/**
 * The sessions block, and the one press in it that answers beside the button:
 * "Выйти со всех других устройств".
 *
 * A refusal to that press has one carrier. The response interceptor raises a
 * plate over the page for a 403, a 429, a 5xx, an expired session and a request
 * that never left, and on a 5xx the plate carries the API's own title - the
 * same string the line beside the button printed. One press, one failure, two
 * red boxes.
 *
 * The line goes and the plate stays, because the list loses nothing: it is
 * untouched in either branch, so the sessions the press failed to end are still
 * listed with their "Завершить" buttons. It is the same rule the per-session
 * press next to them already follows through notifyFailure.
 *
 * What the line still says out loud is everything else - a conflict, a rejected
 * request - because nobody else says it.
 *
 * A session is a framed row of the grey panel: the device, under it
 * "адрес | дата", and "Завершить" beside them on the right. A phone shows the
 * row as the full version does, and the line under the device wraps between
 * its parts rather than cutting the address or putting the date under "Еще".
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import {
  compileSass,
  sassBlocks,
  ruleMap,
  styleRules,
} from "@/shared/lib/testing/styles";
import AccountSessionsSection from "./AccountSessionsSection.vue";
import type { SessionInfo } from "@/shared/api/models/account";

const options = () => ({ global: { plugins: [createPinia()] } });

const FILE = join(
  dirname(fileURLToPath(import.meta.url)),
  "AccountSessionsSection.vue",
);

/** The block's stylesheet as the build compiles it. */
const css = () => compileSass(sassBlocks(FILE), FILE);

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

  // The refusals the interceptor used to plate are said beside the button now,
  // and the plate is taken down (refusalHere).
  it.each([
    [403, "Ошибка сервера"],
    [429, "Слишком много попыток, попробуйте позже"],
    [500, "Ошибка сервера"],
  ])("says a %i beside the button", async (status, line) => {
    const wrapper = await render();
    vi.spyOn(accountApi, "logoutAll").mockResolvedValue({
      data: null,
      error: { status, title: "Ошибка сервера" },
    } as never);

    await logoutAll(wrapper).trigger("click");
    await flushPromises();

    expect(wrapper.get(".error-text").text()).toBe(line);
    // Refused all the same: the other session is still listed and still
    // offers the press that ends it.
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

    expect(wrapper.get(".error-text").text()).toBe("Сессии уже завершены");
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
    expect(wrapper.find(".error-text").exists()).toBe(false);
  });

  // "172.28.0.1|28.09.2026": the bar was a separator of the row's own, a flex
  // item of the line that scrolls, and a flex item loses the spaces at its
  // ends. The line is a caption line now, whose bar stands a space away from
  // either part, as under every post and comment of the site.
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

  // The address of a phone on a mobile network is an IPv6 address of 39
  // characters with no space in it. It is a part of its own, written whole:
  // the line wraps before it, and nothing of it goes under "…" or "Еще".
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

  // "текущая" was a pill beside the device, and the row of this device was
  // filled and framed in blue: the owner, 5 October 2026, "не вписывается в
  // наш дизайн". The session stands apart under a caption of its own instead.
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

      expect(wrapper.find(".current-badge").exists()).toBe(false);
      expect(wrapper.get(".session-device").text()).toBe("Chrome, Windows");
      const [here, elsewhere] = wrapper.findAll(".session-item");
      expect(here.classes()).toEqual(elsewhere.classes());
    });

    it("is painted the way every row is: no fill or frame of the link colour", () => {
      const painted = styleRules(css()).filter(
        ({ selector, declarations }) =>
          /session/.test(selector) &&
          Object.values(declarations).some((value) =>
            value.includes("var(--link)"),
          ),
      );
      expect(painted).toEqual([]);
    });
  });

  // No row of the site scrolls sideways, a caption wraps between its parts,
  // and a phone shows the full version's row rather than a form of its own.
  describe("the row at any width", () => {
    it("scrolls nothing sideways", async () => {
      const wrapper = await render();

      expect(wrapper.find(".scroll-row").exists()).toBe(false);
      for (const line of wrapper.findAll(".session-details"))
        expect(line.classes()).toContain("caption-line");
    });

    // jsdom lays nothing out, so the row is read off the stylesheet: two
    // columns, "Завершить" beside the device, and no narrow form that would
    // put it under the line.
    it("keeps Завершить beside the device at every width", () => {
      const compiled = css();
      expect(compiled).not.toContain("@container");
      expect(compiled).not.toContain("container-type");
      expect(compiled).not.toContain("@media");
      expect(
        ruleMap(compiled).get(".session-item")?.["grid-template-columns"],
      ).toBe("minmax(0, 1fr) auto");
    });
  });
});
