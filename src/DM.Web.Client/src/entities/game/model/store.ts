// The game lists; a single game lives in detailsStore.ts.

import { defineStore } from "pinia";
import { createGameLists } from "./gameLists";
import { createGamesSearch } from "./gamesSearch";

export const useGamesStore = defineStore("games", () => {
  const { lists, invalidateLists } = createGameLists();
  const { search, searchCache } = createGamesSearch();

  return {
    ...lists,

    // Refetches rather than blanks: the sidebar mounts once per session.
    invalidateGameLists: async () => {
      searchCache.clear();
      await invalidateLists();
    },

    ...search,
  };
});
