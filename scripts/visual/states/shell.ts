import { refuse, type State } from "./shared";

export const SHELL_STATES: State[] = [
  // --- One place each ------------------------------------------------------
  // A refusal, a menu, a hint, an edge of the data: each state below opens one
  // place of a page that the states above pass by. They come after those on
  // purpose: the run goes in the file's order, and a page one of them opens is
  // read for the states that follow, so the states above see what they saw.

  // Shell
  {
    name: "guest-home-sidebar-failed",
    role: "guest",
    url: "/",
    act: async (page) => {
      await refuse(page, /\/v1\/polls\?.*status=Active/, "GET", 500, "Ошибка");
      await refuse(page, /\/v1\/games\/tags/, "GET", 500, "Ошибка");
      await page.reload({ waitUntil: "load" });
    },
  },
  {
    name: "guest-home-select-all",
    role: "guest",
    url: "/",
    act: async (page) => {
      await page.locator("body").click({ position: { x: 5, y: 5 } });
      await page.keyboard.press("Control+A");
    },
  },
  {
    // Five presses of Tab from the top of the page: the ring of the focus.
    name: "guest-home-tab-focus",
    role: "guest",
    url: "/",
    act: async (page) => {
      for (let i = 0; i < 5; i++) await page.keyboard.press("Tab");
    },
  },
];
