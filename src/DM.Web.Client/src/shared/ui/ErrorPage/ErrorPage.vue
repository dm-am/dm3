<script setup lang="ts">
import { computed } from "vue";
import { useRoute, useRouter } from "vue-router";
import { getErrorConfig } from "./errorConfig";

const props = defineProps<{ code: number }>();

const router = useRouter();
const route = useRoute();

// 401 links open the auth modal on the current route (GuestActions reads ?action=).
const loginTo = computed(() => ({
  query: { ...route.query, action: "login" },
}));
const registerTo = computed(() => ({
  query: { ...route.query, action: "register" },
}));

const config = computed(() => getErrorConfig(props.code));

const canGoBack = computed(() => !!window.history.state?.back);
const showsBack = computed(
  () => config.value.showBack !== false && canGoBack.value,
);

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
    <page-title>{{ config.title }}</page-title>

    <img
      :src="config.image.src"
      :alt="`Ошибка ${code}`"
      :width="config.image.width"
      :height="config.image.height"
      class="error-image"
    />

    <p v-if="code === 401" class="error-description">
      Эта страница доступна только авторизованным пользователям.
      <router-link :to="loginTo" class="inline-link">Войдите</router-link>
      в аккаунт или
      <router-link :to="registerTo" class="inline-link"
        >зарегистрируйтесь</router-link
      >.
    </p>
    <p v-else class="error-description">{{ config.description }}</p>

    <div class="error-actions">
      <button v-if="showsBack" type="button" class="error-link" @click="goBack">
        Вернуться к предыдущему действию
      </button>

      <span v-if="showsBack" class="action-sep" aria-hidden="true">/</span>

      <router-link to="/" class="error-link">
        Вернуться на главную
      </router-link>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

.error-page
  padding: $medium 0 $big

.error-image
  max-width: 400px
  width: 100%
  height: auto
  margin-bottom: $medium

.error-description
  color: $text
  margin: 0 0 $medium
  line-height: 1.6

.inline-link
  font-weight: 700

.error-actions
  display: flex
  align-items: center
  gap: $small

.action-sep
  color: $text-muted

// A <button> and a router link side by side, one link to the eye.
.error-link
  +inline-link-button
  &
    font-weight: 700
</style>
