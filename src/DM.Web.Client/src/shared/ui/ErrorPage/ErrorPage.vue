<script setup lang="ts">
/**
 * Shared error page component for HTTP error states.
 *
 * Layout:
 *   1. "Упс! {title}!" — playful heading with specific error
 *   2. Error illustration (per code or general-error.png)
 *   3. Description text
 *   4. Action links (back / home) in a row
 */
import { computed } from "vue";
import { useRoute, useRouter } from "vue-router";
import { getErrorConfig } from "./errorConfig";

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

// Single source of truth for title/description/illustration per HTTP code
// (see ./errorConfig).
const config = computed(() => getErrorConfig(props.code));
const resolvedTitle = computed(() => props.title ?? config.value.title);
const resolvedDescription = computed(
  () => props.description ?? config.value.description,
);
const resolvedShowBack = computed(
  () => props.showBack ?? config.value.showBack ?? true,
);
const resolvedImage = computed(() => props.image ?? config.value.image.src);

// Intrinsic illustration dimensions reserve layout space before the image
// loads (prevents CLS). Known only for the config-provided illustration;
// a custom `image` override is a src-only string, so dimensions are omitted
// (the attributes render as undefined) rather than guessed.
const imageWidth = computed(() =>
  props.image ? undefined : config.value.image.width,
);
const imageHeight = computed(() =>
  props.image ? undefined : config.value.image.height,
);

// 401 means the viewer is a guest hitting an authorized-only page — the
// "Войдите"/"зарегистрируйтесь" links are rendered inline inside the
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

    <img
      :src="resolvedImage"
      :alt="`Ошибка ${code}`"
      :width="imageWidth"
      :height="imageHeight"
      class="error-image"
    />

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
