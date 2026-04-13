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
import { useRouter } from "vue-router";

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
    description: "Что-то пошло не так с запросом. Попробуйте вернуться и повторить действие.",
  },
  401: {
    title: "Требуется авторизация",
    description: "Для доступа к этой странице необходимо войти в аккаунт.",
    showBack: false,
  },
  403: {
    title: "Доступ запрещен",
    description: "У вас недостаточно прав для просмотра этой страницы.",
  },
  404: {
    title: "Страница не найдена",
    description: "Возможно, страница была перемещена или удалена.",
  },
  409: {
    title: "Конфликт данных",
    description: "Произошел конфликт при обработке запроса. Попробуйте повторить действие.",
  },
  410: {
    title: "Страница удалена",
    description: "Эта страница больше не существует и была окончательно удалена.",
  },
  500: {
    title: "Ошибка сервера",
    description: "Что-то сломалось на нашей стороне. Попробуйте позже.",
  },
};

const fallbackConfig: ErrorConfig = {
  title: "Неизвестная ошибка",
  description: "Что-то пошло не так. Попробуйте вернуться и повторить действие.",
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

const defaults = computed(() => errorDefaults[props.code] ?? fallbackConfig);
const resolvedTitle = computed(() => props.title ?? defaults.value.title);
const resolvedDescription = computed(() => props.description ?? defaults.value.description);
const resolvedShowBack = computed(() => props.showBack ?? defaults.value.showBack ?? true);

const codeImageMap: Record<number, string> = {
  400: img400,
  401: img401,
  403: img403,
  500: img500,
};

const resolvedImage = computed(
  () => props.image ?? codeImageMap[props.code] ?? imgGeneral,
);

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
      class="error-image"
    />

    <p class="error-description">{{ resolvedDescription }}</p>

    <div class="error-actions">
      <slot />

      <a
        v-if="resolvedShowBack && canGoBack"
        href="#"
        class="error-link"
        @click.prevent="goBack"
      >
        <strong>Вернуться к предыдущему действию</strong>
      </a>

      <span v-if="resolvedShowBack && canGoBack && showHome" class="action-sep">/</span>

      <router-link v-if="showHome" to="/" class="error-link">
        <strong>Вернуться на главную</strong>
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

.error-actions
  display: flex
  align-items: center
  gap: $small

.action-sep
  color: $text-muted

.error-link
  color: $link
  &:hover
    color: $link-hover
</style>
