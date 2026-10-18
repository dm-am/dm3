/**
 * The staff table of the rules page: three columns at every width, as on the
 * full version, its rows plain rows that hold links. A login is whole and on
 * one line: nothing cuts it with "...", no tooltip has to repeat it, and no
 * rule of the table breaks it inside.
 */
import { join } from "node:path";
import { afterEach, describe, expect, it, vi } from "vitest";
import { flushPromises, mount, RouterLinkStub } from "@vue/test-utils";
import { UserRole } from "@/shared/api/models/community";
import type { ExpandableListColumn } from "@/shared/ui/ExpandableList";
import { Tooltip } from "@/shared/ui/Tooltip";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";
import RulesStaffTable from "./RulesStaffTable.vue";

const LONG = "Многоуважаемый_Шкаф1";
/** A login may hold single spaces (UsernamePolicy). */
const SPACED = "Темный Властелин";

vi.mock("@/entities/user", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/entities/user")>()),
  userApi: {
    getUsersByRole: vi.fn(async (role: UserRole) => ({
      data: {
        resources:
          role === UserRole.Moderator
            ? [
                { username: "TestModerator", role },
                { username: LONG, role },
                { username: SPACED, role },
              ]
            : [],
      },
    })),
  },
}));

async function mountTable() {
  const wrapper = mount(RulesStaffTable, {
    global: { stubs: { RouterLink: RouterLinkStub } },
    attachTo: document.body,
  });
  await flushPromises();
  return wrapper;
}

afterEach(() => {
  vi.restoreAllMocks();
  document.body.innerHTML = "";
});

describe("RulesStaffTable", () => {
  it("stands its three columns in the row, its rows plain rows", async () => {
    const wrapper = await mountTable();
    // By name: a generic component is no selector to the type checker.
    const list = wrapper.getComponent({ name: "ExpandableList" });
    const columns = list.props("columns") as ExpandableListColumn[];

    expect(columns).toEqual([
      { key: "title", label: "Роль" },
      { key: "description", label: "Функции" },
      { key: "users", label: "Состав" },
    ]);
    expect(list.props("plainRows")).toBe(true);
    expect(wrapper.findAll(".expandable-header .header-cell")).toHaveLength(3);
    wrapper.unmount();
  });

  it("shows every login whole, one to a line, with no tooltip to repeat it", async () => {
    const wrapper = await mountTable();
    const logins = wrapper.findAll(".user-item");

    expect(logins.map((login) => login.text())).toEqual([
      "TestModerator",
      LONG,
      SPACED,
    ]);
    expect(wrapper.findAllComponents(Tooltip)).toHaveLength(0);

    // Nothing in the table's own sheet cuts a line or keeps it from breaking.
    const css = componentCss(join(__dirname, "RulesStaffTable.vue"));
    expect(css).not.toMatch(/text-overflow|nowrap|overflow:\s*hidden/);
    wrapper.unmount();
  });

  // A login stands on one line, as at 1366: no rule of the table breaks it by
  // its letters or balances its lines.
  it("breaks no login inside", async () => {
    const wrapper = await mountTable();
    const rules = ruleMap(componentCss(join(__dirname, "RulesStaffTable.vue")));

    expect(rules.get(".user-item")).not.toHaveProperty("word-break");
    expect(rules.get(".user-item")).not.toHaveProperty("text-wrap");
    expect(rules.has(".user-item--word")).toBe(false);
    expect(wrapper.find(".user-item--word").exists()).toBe(false);
    wrapper.unmount();
  });

  // The table is the last block of the rules page, and the page ends where it
  // ends: the shell keeps the one space between a page and what stands under
  // it. A margin of the section under it stood on top of that, and the rules
  // ended 80 above it.
  it("keeps no space of its own under it: the shell's is the one", () => {
    const rules = ruleMap(componentCss(join(__dirname, "RulesStaffTable.vue")));

    expect(rules.get(".admin-section")).toEqual({ "margin-top": "32px" });
  });
});
