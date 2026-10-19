// The staff table: every login whole and no tooltip repeats it; its look is the visual stand's to guard.
import { afterEach, describe, expect, it, vi } from "vitest";
import { flushPromises, mount, RouterLinkStub } from "@vue/test-utils";
import { userApi } from "@/entities/user";
import { UserRole } from "@/shared/api/models/community";
import { ErrorState } from "@/shared/ui/ErrorState";
import { Tooltip } from "@/shared/ui/Tooltip";
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
  it("shows every login whole, with no tooltip to repeat it", async () => {
    const wrapper = await mountTable();
    const logins = wrapper.findAll(".user-item");

    expect(logins.map((login) => login.text())).toEqual([
      "TestModerator",
      LONG,
      SPACED,
    ]);
    expect(wrapper.findAllComponents(Tooltip)).toHaveLength(0);
    wrapper.unmount();
  });

  // The site's error block, not a line of its own in every cell; the roles stay, their column of members goes.
  it("says a failed load once under the table and loads again on Повторить", async () => {
    vi.mocked(userApi.getUsersByRole).mockResolvedValueOnce({
      error: { title: "Сервер недоступен" },
    } as never);
    const wrapper = await mountTable();

    const failure = wrapper.findComponent(ErrorState);
    expect(failure.props("message")).toBe("Сервер недоступен");
    expect(wrapper.text()).not.toContain("Состав");
    expect(wrapper.text()).toContain("Функции");

    await failure.find("button").trigger("click");
    await flushPromises();

    expect(wrapper.findComponent(ErrorState).exists()).toBe(false);
    expect(wrapper.text()).toContain("Состав");
    expect(wrapper.findAll(".user-item")).toHaveLength(3);
    wrapper.unmount();
  });

  // A role and a login take what they hold, the functions take the rest: no column gets a third by default.
  it("sizes Роль and Состав by what they hold and leaves the rest to Функции", async () => {
    const wrapper = await mountTable();
    // By name: the list is generic, and the typed lookup has no overload for it.
    const list = wrapper.findComponent({ name: "ExpandableList" });
    const columns = list.props("columns") as {
      label: string;
      fit?: boolean;
      width?: string;
    }[];

    expect(
      columns.map(({ label, fit, width }) => ({ label, fit, width })),
    ).toEqual([
      { label: "Роль", fit: true, width: undefined },
      { label: "Функции", fit: undefined, width: undefined },
      { label: "Состав", fit: true, width: undefined },
    ]);
    wrapper.unmount();
  });
});
