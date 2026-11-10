import {
  ADMIN_GAME,
  DECLINED_CHARACTER,
  OWNED_GAME,
  refuse,
  type State,
} from "./shared";

export const CHARACTERS_STATES: State[] = [
  // Characters
  {
    name: "member-game-character",
    role: "user",
    url: (f) => `/game/${f.playedGame}/characters/${f.characterId}`,
  },
  {
    name: "member-game-character-failed",
    role: "user",
    url: (f) => `/game/${f.playedGame}/characters/${f.characterId}`,
    act: async (page) => {
      await refuse(
        page,
        /\/v1\/characters\/[0-9a-f-]{36}$/,
        "GET",
        500,
        "Ошибка",
      );
      await page.reload({ waitUntil: "load" });
    },
  },
  {
    name: "guest-game-characters",
    role: "guest",
    url: `/game/${ADMIN_GAME}/characters`,
  },
  {
    name: "guest-game-character-declined",
    role: "guest",
    url: `/game/${ADMIN_GAME}/characters/${DECLINED_CHARACTER}`,
  },
  {
    // The master reads both notepads of a character of his game.
    name: "admin-game-character",
    role: "admin",
    url: (f) => `/game/${ADMIN_GAME}/characters/${f.uiCharacterId}`,
  },
  {
    name: "member-owned-game-characters",
    role: "user",
    url: `/game/${OWNED_GAME}/characters`,
  },
  {
    name: "member-owned-game-npc",
    role: "user",
    url: (f) => `/game/${OWNED_GAME}/characters/${f.npcId}`,
  },
];
