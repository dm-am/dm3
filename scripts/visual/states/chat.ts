import { hoverUntilTooltip, LIKED, type State } from "./shared";

export const CHAT_STATES: State[] = [
  // Chat
  {
    name: "member-messenger-search",
    role: "user",
    url: "/messenger",
    whole: false,
    act: async (page) => {
      await page
        .getByRole("combobox", { name: "Поиск собеседника" })
        .fill("Pla");
      await page.locator(".autocomplete-item").first().waitFor();
    },
  },
  {
    // The whole page: in a window of the real size the composer, focused
    // after the load in some runs only, scrolls the page under the tooltip.
    name: "member-global-chat-like-tooltip",
    role: "user",
    url: "/global-chat",
    storage: { dm_message_layout: "full" },
    act: (page) => hoverUntilTooltip(page, LIKED, "Нравится:"),
  },
  {
    name: "member-global-chat-search-empty",
    role: "user",
    url: "/global-chat",
    whole: false,
    act: async (page) => {
      await page.getByPlaceholder(/Поиск/).first().fill("яяяя");
      await page.getByText("Ничего не найдено").waitFor();
    },
  },
];
