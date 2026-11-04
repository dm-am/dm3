import type { RouteRecordRaw } from "vue-router";

export const gameRoutes: RouteRecordRaw[] = [
  {
    name: "games",
    path: "/games",
    meta: { title: "Игры" },
    component: () => import("@/pages/game/GamesPage.vue"),
  },
  {
    name: "create-game",
    path: "/games/create",
    // No requiresAuth: the page renders its own login invitation for guests
    meta: { title: "Новая игра" },
    component: () => import("@/pages/create-game/CreateGamePage.vue"),
  },
  {
    // GamePage owns the tab title: the game name, then the section. A section
    // that is data (a room, a character) is composed by the page.
    path: "/game/:id",
    meta: { gameZone: true },
    component: () => import("@/pages/game/GamePage.vue"),
    children: [
      {
        name: "game",
        path: "",
        meta: { dynamicTitle: true },
        component: () => import("@/pages/game/GameDetails.vue"),
      },
      // The room of posts is a record of its own in router.ts: no right column.
      {
        // A chat room (RoomType.Chat), apart from the post room view.
        name: "game-chat-room",
        path: "chat-rooms/:num",
        meta: { dynamicTitle: true },
        component: () => import("@/pages/game/GameChatRoom.vue"),
      },
      {
        name: "game-characters",
        path: "characters",
        meta: { section: "Персонажи" },
        component: () => import("@/pages/game/GameCharacters.vue"),
      },
      {
        name: "game-character-create",
        path: "characters/create",
        // The section is the ?npc flag, which meta cannot spell.
        meta: { requiresAuth: true, dynamicTitle: true },
        component: () => import("@/pages/game/CharacterCreate.vue"),
      },
      {
        // After the static "characters/create"; the section is the name.
        name: "game-character",
        path: "characters/:characterId",
        meta: { dynamicTitle: true },
        component: () => import("@/pages/game/GameCharacter.vue"),
      },
      {
        name: "game-character-edit",
        path: "characters/:characterId/edit",
        meta: { requiresAuth: true, section: "Редактирование персонажа" },
        component: () => import("@/pages/game/CharacterEdit.vue"),
      },
      {
        name: "game-settings",
        path: "settings",
        meta: { section: "Настройки" },
        component: () => import("@/pages/game/GameSettings.vue"),
      },
      {
        name: "game-comments",
        path: "comments",
        meta: { section: "Обсуждение" },
        component: () => import("@/pages/game/GameComments.vue"),
      },
      {
        name: "game-reviews",
        path: "reviews",
        meta: { section: "Рецензии" },
        component: () => import("@/pages/game/GameReviews.vue"),
      },
      {
        name: "game-post-reviews",
        path: "post-reviews",
        meta: { section: "Оцененные посты" },
        component: () => import("@/pages/game/GamePostReviews.vue"),
      },
      {
        name: "game-notepad",
        path: "notes",
        meta: { section: "Заметки игры" },
        component: () => import("@/pages/game/GameNotepad.vue"),
      },
    ],
  },
  {
    name: "game-first-unread-post",
    path: "/game/:id/posts/unread",
    // Outside the game shell, so it carries a title of its own.
    meta: { title: "Переход к непрочитанному посту" },
    component: () => import("@/pages/game/GameFirstUnreadPost.vue"),
  },
  {
    name: "game-first-unread-comment",
    path: "/game/:id/comments/unread",
    meta: { title: "Переход к непрочитанному комментарию" },
    component: () => import("@/pages/game/GameFirstUnreadComment.vue"),
  },
];
