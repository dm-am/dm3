<script setup lang="ts">
/**
 * Shared error page component for HTTP error states.
 *
 * Layout:
 *   1. «Упс! {title}!» — playful heading with specific error
 *   2. Error illustration (per code or general-error.png)
 *   3. Description text
 *   4. Action links (back / home) in a row
 */
import { computed } from "vue";
import { useRoute, useRouter } from "vue-router";

import img400 from "@/assets/images/errors/400.png";
import img401 from "@/assets/images/errors/401.png";
import img403 from "@/assets/images/errors/403.png";
import img500 from "@/assets/images/errors/500.png";
import imgGeneral from "@/assets/images/errors/general-error.png";

interface ErrorConfig {
  title: string;
  description: string;
  showBack?: boolean;
}

const errorDefaults: Record<number, ErrorConfig> = {
  400: {
    title: "Неверный запрос",
    description:
      "Сервер не понял запрос. Проверьте введенные данные и попробуйте снова.",
  },
  401: {
    title: "Требуется авторизация",
    description:
      "Эта страница доступна только авторизованным пользователям. Войдите в аккаунт или зарегистрируйтесь.",
    showBack: false,
  },
  403: {
    title: "Доступ запрещен",
    description:
      "У вас недостаточно прав для просмотра этой страницы. Возможно, доступ ограничен автором или администрацией.",
  },
  404: {
    title: "Страница не найдена",
    description:
      "Такой страницы не существует. Возможно, она была перемещена или удалена.",
  },
  409: {
    title: "Конфликт данных",
    description:
      "При обработке запроса произошел конфликт. Попробуйте повторить действие.",
  },
  410: {
    title: "Страница удалена",
    description:
      "Этой страницы больше не существует. Возможно, она была удалена автором или модератором.",
  },
  500: {
    title: "Произошла ошибка сервера",
    description: "Что-то сломалось на нашей стороне. Попробуйте немного позже.",
  },
};

const fallbackConfig: ErrorConfig = {
  title: "Неизвестная ошибка",
  description:
    "Что-то пошло не так. Попробуйте вернуться и повторить действие.",
};

const props = withDefaults(
  defineProps<{
    code: number;
    title?: string;
    description?: string;
    image?: string;
    showBack?: boolean;
    showHome?: boolean;
  }>(),
  {
    showHome: true,
  },
);

const router = useRouter();
const route = useRoute();

// 401 login/register links open the auth modal on the CURRENT route (the
// GuestActions widget reads ?action=…) instead of sending the user to home.
const loginTo = computed(() => ({
  query: { ...route.query, action: "login" },
}));
const registerTo = computed(() => ({
  query: { ...route.query, action: "register" },
}));

const defaults = computed(() => errorDefaults[props.code] ?? fallbackConfig);
const resolvedTitle = computed(() => props.title ?? defaults.value.title);
const resolvedDescription = computed(
  () => props.description ?? defaults.value.description,
);
const resolvedShowBack = computed(
  () => props.showBack ?? defaults.value.showBack ?? true,
);

// TODO(round-2 art): dedicated illustrations for 404 / 409 / 410 are missing.
// They currently fall back to general-error.png. Add 404 (empty chest),
// 409 (fighting goblins), 410 (ashes) per docs/plans/ERROR_PAGES_AND_LORE.md
// and extend this map. 404 is the most visited error page (catch-all route).
const codeImageMap: Record<number, string> = {
  400: img400,
  401: img401,
  403: img403,
  500: img500,
};

const resolvedImage = computed(
  () => props.image ?? codeImageMap[props.code] ?? imgGeneral,
);

// 401 means the viewer is a guest hitting an authorized-only page — the
// «Войдите»/«зарегистрируйтесь» links are rendered inline inside the
// description sentence (see template), not as a separate action row.
const canGoBack = computed(() => !!window.history.state?.back);

function goBack() {
  if (canGoBack.value) {
    router.back();
  } else {
    router.push("/");
  }
}
</script>

<template>
  <div class="error-page">
    <page-title>Упс! {{ resolvedTitle }}!</page-title>

    <img :src="resolvedImage" :alt="`Ошибка ${code}`" class="error-image" />

    <!-- 401: login/register are inline links inside the sentence. -->
    <p v-if="code === 401" class="error-description">
      Эта страница доступна только авторизованным пользователям.
      <router-link :to="loginTo" class="inline-link">Войдите</router-link>
      в аккаунт или
      <router-link :to="registerTo" class="inline-link"
        >зарегистрируйтесь</router-link
      >.
    </p>
    <p v-else class="error-description">{{ resolvedDescription }}</p>

    <div class="error-actions">
      <slot />

      <button
        v-if="resolvedShowBack && canGoBack"
        type="button"
        class="error-link"
        @click="goBack"
      >
        Вернуться к предыдущему действию
      </button>

      <span
        v-if="resolvedShowBack && canGoBack && showHome"
        class="action-sep"
        aria-hidden="true"
        >/</span
      >

      <router-link v-if="showHome" to="/" class="error-link">
        Вернуться на главную
      </router-link>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.error-page
  padding: $medium 0 $big

.error-image
  max-width: 400px
  width: 100%
  height: auto
  margin-bottom: $medium
  border-radius: $border-radius

.error-description
  color: $text
  margin: 0 0 $medium
  line-height: 1.6

.inline-link
  color: $link
  font-weight: 700
  text-decoration: none
  &:hover
    color: $link-hover
    text-decoration: underline

.error-actions
  display: flex
  align-items: center
  gap: $small

.action-sep
  color: $text-muted

.error-link
  font: inherit
  font-weight: 700
  color: $link
  text-decoration: none
  cursor: pointer
  // Reset so a <button> styled as a link is indistinguishable from <a>.
  padding: 0
  border: none
  background: none
  &:hover
    color: $link-hover
</style>
