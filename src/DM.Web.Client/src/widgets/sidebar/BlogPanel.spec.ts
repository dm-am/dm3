/**
 * @vitest-environment jsdom
 */

/** The blog menu, to the game menu's plan: one heading, the rubrics nested, every page with a counter. */
import { describe, it, expect, afterEach, beforeEach, vi } from "vitest";
import { flushPromises, mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import BlogPanel from "./BlogPanel.vue";
import SidebarSectionTitle from "./SidebarSectionTitle.vue";
import { blogApi, useBlogDetailsStore, type Blog } from "@/entities/blog";
import { useAuthStore } from "@/entities/user";
import { UserRole, type PremoderationStatus } from "@/shared/api/models/common";

const stubs = {
  RouterLink: RouterLinkStub,
  Tooltip: { template: "<span><slot /></span>", props: ["text"] },
  ConfirmDialog: true,
};

const blog = {
  id: "b-1",
  publicId: "aaaab",
  title: "Дневник приключенца",
  status: "Active",
  author: { id: "u-1", username: "Автор" },
  assistants: [],
  unreadPublicationsCount: 6,
  unreadCommentsCount: 0,
  rubrics: [
    {
      id: "r-2",
      title: "Для избранных",
      sortOrder: 2,
      publicationCount: 0,
      unreadPublicationsCount: 0,
      unreadCommentsCount: 0,
    },
    {
      id: "r-1",
      title: "Общее",
      sortOrder: 1,
      publicationCount: 6,
      unreadPublicationsCount: 6,
      unreadCommentsCount: 3,
    },
  ],
} as unknown as Blog;

function mountPanel(
  premoderationStatus?: PremoderationStatus,
  overrides: Record<string, unknown> = {},
) {
  useBlogDetailsStore().blog = { ...blog, premoderationStatus };
  // The join row reads the readers when it mounts: nobody reads this blog.
  vi.spyOn(blogApi, "getReaders").mockResolvedValue({
    data: { resources: [] },
    error: null,
  } as never);
  return mount(BlogPanel, {
    props: { blogId: "aaaab" },
    global: { stubs: { ...stubs, ...overrides } },
  });
}

function signedInAs(username: string, role?: UserRole) {
  const auth = useAuthStore();
  auth.user = { username, role } as unknown as NonNullable<typeof auth.user>;
}

/**
 * What a row copies as: a non-breaking space is still a space, and a run of
 * whitespace is one space — the markup indents a button's caption onto its own
 * line, and the browser collapses that the same way this does.
 */
const copied = (text: string) =>
  text
    .replace(/\u00a0/g, " ")
    .replace(/\s+/g, " ")
    .trim();

/** Every menu row, in the order the menu renders them. */
function rows(wrapper: ReturnType<typeof mountPanel>) {
  return wrapper.findAll("li.link").map((li) => copied(li.text()));
}

describe("BlogPanel", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("heads the block with the menu's own name, not the blog's", () => {
    const wrapper = mountPanel();
    expect(wrapper.find(".sidebar-title").text()).toBe("Меню блога");
    expect(wrapper.find(".sidebar-title a").exists()).toBe(false);
    expect(wrapper.find(".toggle").attributes("aria-label")).toBe(
      'Свернуть раздел "Меню блога"',
    );
  });

  it("lists the menu in one order, counters and all", () => {
    expect(rows(mountPanel())).toEqual([
      "- Рубрики",
      "Общее (6/3)",
      "Для избранных (0/0)",
      "- Информация",
      "- Лента публикаций (6)",
      "- Обсуждение (0)",
    ]);
  });

  it("nests the rubrics under their group row", () => {
    const wrapper = mountPanel();
    const nested = wrapper.findAll("ul.rubric-list");
    expect(nested).toHaveLength(1);
    expect(nested[0].findAll("li.link")).toHaveLength(2);
  });

  it("heads its sections the way the game panel does", () => {
    signedInAs("Автор");
    const headings = mountPanel().findAllComponents(SidebarSectionTitle);
    expect(headings.map((heading) => heading.text())).toEqual([
      "Управление блогом",
    ]);
  });

  // Three moves to two audiences; a row shown to anybody else would be answered 403 or 400.
  describe("premoderation", () => {
    const SUBMIT = "- Отправить на проверку";
    const APPROVE = "- Одобрить блог";
    const RETURN = "- Вернуть на доработку";

    it("offers the owner the submit row while the blog is on edits", () => {
      signedInAs("Автор");
      expect(rows(mountPanel("AwaitingEdits"))).toContain(SUBMIT);
    });

    it.each([
      ["AwaitingApproval", "AwaitingApproval" as PremoderationStatus],
      ["Approved", "Approved" as PremoderationStatus],
      ["not sent at all", undefined],
    ])(
      "hides the submit row from the owner when the status is %s",
      (_name, status) => {
        signedInAs("Автор");
        expect(rows(mountPanel(status))).not.toContain(SUBMIT);
      },
    );

    // BlogIntention.SubmitForApproval admits the owner alone.
    it("hides the submit row from a reader on edits", () => {
      signedInAs("Читатель");
      expect(rows(mountPanel("AwaitingEdits"))).not.toContain(SUBMIT);
    });

    it("hides the submit row from a site mentor who is not the owner", () => {
      signedInAs("Наставник", UserRole.Mentor);
      expect(rows(mountPanel("AwaitingEdits"))).not.toContain(SUBMIT);
    });

    // Both verdicts are legal from every status.
    it.each([
      ["Approved" as PremoderationStatus],
      ["AwaitingApproval" as PremoderationStatus],
      ["AwaitingEdits" as PremoderationStatus],
    ])("gives a site mentor both verdicts from %s", (status) => {
      signedInAs("Наставник", UserRole.Mentor);
      const menu = rows(mountPanel(status));

      expect(menu).toContain(APPROVE);
      expect(menu).toContain(RETURN);
    });

    it("keeps the verdicts away from the owner", () => {
      signedInAs("Автор");
      const menu = rows(mountPanel("AwaitingEdits"));

      expect(menu).not.toContain(APPROVE);
      expect(menu).not.toContain(RETURN);
    });
  });
});

/** The question before a moderation move waits for the answer and says a refusal in itself. */
describe("BlogPanel moderation question", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  afterEach(() => {
    document.body.innerHTML = "";
  });

  it("keeps the question open with the refusal in it", async () => {
    signedInAs("Наставник", UserRole.Mentor);
    const store = useBlogDetailsStore();
    const change = vi.spyOn(store, "changePremoderation").mockResolvedValue({
      type: "",
      title: "",
      status: 404,
      traceId: "t",
    });
    const wrapper = mountPanel("AwaitingApproval", { ConfirmDialog: false });

    const approve = wrapper
      .findAll("button")
      .find((b) => b.text() === "Одобрить блог");
    await approve!.trigger("click");
    document.body
      .querySelector<HTMLButtonElement>(".dialog-btn-submit")
      ?.click();
    await flushPromises();

    expect(change).toHaveBeenCalledWith("SetApproved", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(
      document.body.querySelector(".dialog-refusal")?.textContent?.trim(),
    ).toBe("Не удалось изменить премодерацию");
    expect(document.body.textContent).toContain("Одобрить блог?");
    wrapper.unmount();
  });
});

/** "Обсуждение" stays a row for the one on the blacklist: it closes writing, not reading. */
describe("BlogPanel discussion row", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  const hasRow = (wrapper: ReturnType<typeof mountPanel>) =>
    rows(wrapper).some((row) => row.startsWith("- Обсуждение"));

  it("keeps the row of an empty discussion to the one on its blacklist", async () => {
    signedInAs("Читатель");
    const wrapper = mountPanel();
    useBlogDetailsStore().blog = {
      ...blog,
      commentsCount: 0,
      viewer: { canComment: false },
      viewerBlacklisted: true,
    } as unknown as Blog;
    await flushPromises();

    expect(hasRow(wrapper)).toBe(true);
  });
});
