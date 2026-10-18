/**
 * @vitest-environment jsdom
 */

/**
 * The blog menu is built to the same plan as the game menu, and the plan is
 * the requirement: one fixed heading, the rubric group with its rubrics
 * nested under it, then the blog's pages, each with a counter and no
 * exception at zero.
 *
 * It had drifted on every one of those at once. The heading was the blog's
 * own title, and a link. "Рубрики" and "Обсуждение" were bold muted capitals
 * rather than menu rows. "Информация" and the feed were not in the menu at
 * all: the heading link stood in for one, a line under the rubric table for
 * the other. The discussion counter vanished at zero. And the panel repeated
 * the status that the info table prints two lines further down.
 */
import { describe, it, expect, afterEach, beforeEach, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import BlogPanel from "./BlogPanel.vue";
import SidebarSectionTitle from "./SidebarSectionTitle.vue";
import {
  useBlogDetailsStore,
  type Blog,
  type BlogPremoderationStatus,
} from "@/entities/blog";
import { useAuthStore } from "@/entities/user";
import { UserRole } from "@/shared/api/models/common";

const stubs = {
  "router-link": {
    template: '<a class="router-link"><slot /></a>',
    props: ["to"],
  },
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
  premoderationStatus?: BlogPremoderationStatus,
  overrides: Record<string, unknown> = {},
) {
  useBlogDetailsStore().blog = { ...blog, premoderationStatus };
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

  it("leaves the blog's status to the info table", () => {
    expect(mountPanel().text()).not.toContain("Статус");
  });

  it("heads its sections the way the game panel does", () => {
    signedInAs("Автор");
    const headings = mountPanel().findAllComponents(SidebarSectionTitle);
    expect(headings.map((heading) => heading.text())).toEqual([
      "Управление блогом",
    ]);
  });

  // Premoderation, mirroring the game panel: the server grants exactly three
  // moves to two different audiences, and a row shown to anybody else is a
  // promise the server answers with 403 or 400.
  describe("premoderation", () => {
    const SUBMIT = "- Отправить на проверку";
    const APPROVE = "- Одобрить блог";
    const RETURN = "- Вернуть на доработку";

    it("offers the owner the submit row while the blog is on edits", () => {
      signedInAs("Автор");
      expect(rows(mountPanel("AwaitingEdits"))).toContain(SUBMIT);
    });

    it.each([
      ["AwaitingApproval", "AwaitingApproval" as BlogPremoderationStatus],
      ["Approved", "Approved" as BlogPremoderationStatus],
      ["not sent at all", undefined],
    ])(
      "hides the submit row from the owner when the status is %s",
      (_name, status) => {
        signedInAs("Автор");
        expect(rows(mountPanel(status))).not.toContain(SUBMIT);
      },
    );

    // BlogIntention.SubmitForApproval admits the owner alone: an assistant
    // writes in the blog, the mentor approves its publications, and neither
    // declares the blog itself ready.
    it("hides the submit row from a reader on edits", () => {
      signedInAs("Читатель");
      expect(rows(mountPanel("AwaitingEdits"))).not.toContain(SUBMIT);
    });

    it("hides the submit row from a site mentor who is not the owner", () => {
      signedInAs("Наставник", UserRole.Mentor);
      expect(rows(mountPanel("AwaitingEdits"))).not.toContain(SUBMIT);
    });

    // Both verdicts are legal from every status on the server, so neither row
    // is keyed on the status the blog happens to be in.
    it.each([
      ["Approved" as BlogPremoderationStatus],
      ["AwaitingApproval" as BlogPremoderationStatus],
      ["AwaitingEdits" as BlogPremoderationStatus],
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

    // The mentor-only round trip the machine no longer has. Its two rows named
    // transitions the server has deleted, so both were a guaranteed 400.
    it("no longer offers the old take-in / release pair", () => {
      signedInAs("Админ", UserRole.Admin);
      const menu = rows(mountPanel("AwaitingEdits"));

      expect(menu).not.toContain("- Отправить на премодерацию");
      expect(menu).not.toContain("- Снять с премодерации");
    });
  });
});

/**
 * The question before a moderation move waits for the answer and says a
 * refusal in itself. It used to close before the request went out, so a
 * refused verdict came back as a plate over a menu that looked as if nothing
 * had been asked.
 */
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

/**
 * "Обсуждение" is a row whatever closes the discussion to the viewer's
 * writing: the blacklist closes it to writing and not to reading, so the one
 * on the list reads it too, empty or not, and the menu leads him there.
 */
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
