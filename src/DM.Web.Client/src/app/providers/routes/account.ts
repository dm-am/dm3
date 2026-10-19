import type { RouteRecordRaw } from "vue-router";

export const accountRoutes: RouteRecordRaw[] = [
  {
    name: "account",
    path: "/account",
    meta: { requiresAuth: true, title: "Настройки аккаунта" },
    component: () => import("@/pages/account/AccountPage.vue"),
  },
  {
    name: "activation",
    path: "/activate",
    meta: { title: "Активация аккаунта", fromLetter: true },
    component: () => import("@/pages/account/AccountActivationPage.vue"),
  },
  {
    name: "confirm-email",
    path: "/confirm-email",
    meta: { title: "Подтверждение почты", fromLetter: true },
    component: () => import("@/pages/account/EmailChangePage.vue"),
  },
  {
    name: "reset-password",
    path: "/reset-password",
    meta: { title: "Сброс пароля", fromLetter: true },
    component: () => import("@/pages/account/PasswordResetPage.vue"),
  },
  // Open to a guest: the letter is often opened where nobody is signed in.
  {
    name: "change-username",
    path: "/change-username",
    meta: { title: "Смена имени", fromLetter: true },
    component: () => import("@/pages/account/UsernameChangePage.vue"),
  },
  // The paths of the letters about taking a second factor off, composed by
  // TwoFactorRemovalMailSender on the server; a guest reaches both.
  {
    name: "two-factor-removal",
    path: "/two-factor/removal",
    meta: {
      title: "Отключение двухфакторной аутентификации",
      fromLetter: true,
    },
    component: () => import("@/pages/account/TwoFactorRemovalPage.vue"),
  },
  {
    name: "two-factor-removal-cancel",
    path: "/two-factor/removal/cancel",
    meta: {
      title: "Отмена отключения двухфакторной аутентификации",
      fromLetter: true,
    },
    component: () => import("@/pages/account/TwoFactorRemovalCancelPage.vue"),
  },
];
