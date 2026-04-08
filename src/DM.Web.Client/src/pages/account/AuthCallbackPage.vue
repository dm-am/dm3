<script setup lang="ts">
import { onMounted } from "vue";
import { useRouter } from "vue-router";
import Button from "@/shared/ui/Button/Button.vue";

const router = useRouter();

// OAuth callback page - currently not in use
// This page exists as a stub for future OAuth provider integrations
// (e.g., Discord, Google OAuth)
onMounted(() => {
  // Redirect to home after delay
  setTimeout(() => {
    router.push("/");
  }, 3000);
});

function goHome() {
  router.push("/");
}
</script>

<template>
  <div class="auth-callback">
    <div class="callback-message">
      <page-title>OAuth не настроен</page-title>
      <p>Внешняя авторизация (Discord, Google) пока не реализована.</p>
      <p class="redirect-text">Перенаправляем на главную...</p>
      <Button @click="goHome">На главную</Button>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.auth-callback
  display: flex
  justify-content: center
  align-items: center
  min-height: $grid-step * 100
  padding: $big

.callback-message
  text-align: center
  max-width: $grid-step * 100
  padding: $big
  background-color: $bg-element
  border-radius: $border-radius

  h2
    margin-bottom: $medium
    color: $text

  p
    margin-bottom: $medium
    color: $text-muted

.redirect-text
  font-style: italic
</style>
