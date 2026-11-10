import {
  answer,
  button,
  hoverUntilTooltip,
  LIKED,
  minutesAfter,
  refuse,
  type State,
} from "./shared";

export const BLOGS_FORUM_STATES: State[] = [
  // Blogs and forum
  {
    name: "guest-blog-rubric-hover",
    role: "guest",
    url: "/blogs/aaaaa",
    whole: false,
    act: async (page) => {
      await page.getByRole("link", { name: "Для избранных" }).first().hover();
    },
  },
  {
    name: "member-blog-other",
    role: "user",
    url: "/blogs/aaaaa",
  },
  {
    // The invitations of the seed are swept a week after it was laid out, and
    // a stand seeded on the pinned epoch has them swept at once: the list is
    // answered here, in the shape the server gives (no role of the invited).
    name: "member-blog-settings-invitations",
    role: "user",
    url: (f) => `/blogs/${f.blogId}/settings`,
    act: async (page) => {
      const invited = (n: number, username: string) => ({
        id: `00000000-0000-0000-0000-0000000000b${n}`,
        blogId: "00000000-0000-0000-0000-0000000000b0",
        blogTitle: "Блог для кадра",
        invitedUser: {
          id: `00000000-0000-0000-0000-0000000000c${n}`,
          username,
          lastActivityUtc: "2026-01-01T00:00:00Z",
        },
        inviterUsername: "TestUser",
        type: "reader",
        createdUtc: "2026-01-01T00:00:00Z",
        expiresUtc: "2026-02-01T00:00:00Z",
      });
      await answer(page, /\/v1\/blogs\/[^/]+\/invitations$/, {
        resources: [invited(1, "SolohinLex"), invited(2, "TestMentor")],
        paging: null,
      });
      await page.reload({ waitUntil: "load" });
    },
  },
  {
    name: "member-forum-index",
    role: "user",
    url: "/forum",
  },
  {
    // TestModerator moderates the board: the pinned topics carry the handles.
    name: "moderator-forum-board",
    role: "moderator",
    url: "/forum/general",
  },
  {
    name: "guest-forum-news",
    role: "guest",
    url: "/forum/news",
  },
  {
    // Opened by its title: the number of the topic follows the digests the API
    // writes when it starts.
    name: "guest-forum-news-topic",
    role: "guest",
    url: "/forum/news",
    act: async (page) => {
      await page
        .getByRole("link", { name: "Запуск новой версии Dungeon Master!" })
        .click();
    },
  },
  {
    // The clock five minutes after the member's comment, inside the window in
    // which he may edit it, and then past that window.
    name: "member-forum-comment-own",
    role: "user",
    url: (f) => f.ownCommentTopicUrl,
    clock: (f) => minutesAfter(f.ownCommentCreatedUtc, 5),
  },
  {
    name: "member-forum-comment-own-late",
    role: "user",
    url: (f) => f.ownCommentTopicUrl,
    clock: (f) => minutesAfter(f.ownCommentCreatedUtc, 20),
  },
  {
    // The search over the comments applies when its box loses the focus.
    name: "member-forum-comments-wildcard",
    role: "user",
    url: "/forum/general/4",
    act: async (page) => {
      const search = page.getByPlaceholder("Поиск по тексту");
      await search.fill("%");
      await search.blur();
    },
  },
  {
    // Likes of the seed are drawn, so the first count above zero is hovered:
    // its tooltip names who likes it.
    name: "member-forum-like-tooltip",
    role: "user",
    url: "/forum/general/4",
    whole: false,
    act: (page) => hoverUntilTooltip(page, LIKED, "Нравится:"),
  },
  {
    name: "member-forum-comment-refused",
    role: "user",
    url: "/forum/general/4",
    act: async (page) => {
      // An empty title: the page falls back to a sentence of its own.
      await refuse(page, /\/v1\/topics\/[^/]+\/comments$/, "POST", 500, "");
      await button(page, "Режим BBCode").click();
      await page
        .getByRole("textbox", { name: "Написать комментарий..." })
        .fill("тест");
      await button(page, "Отправить").click();
    },
  },
  {
    name: "moderator-comment-warn-dialog",
    role: "moderator",
    url: "/forum/general/4",
    whole: false,
    act: async (page) => {
      await page.getByText("Предупреждение", { exact: true }).first().click();
      await page.getByRole("dialog").waitFor();
    },
  },
];
