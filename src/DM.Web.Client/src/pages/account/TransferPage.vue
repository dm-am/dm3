<script setup lang="ts">
import { onMounted } from "vue";
import { useRouter } from "vue-router";

const router = useRouter();

// Session transfer is not implemented - just redirect home
// This page exists as a stub for future implementation
onMounted(() => {
  setTimeout(() => {
    router.push("/");
  }, 1500);
});
</script>

<template>
  <div class="transfer-page">
    <div class="transfer-message">
      <page-title>Переход на зеркало</page-title>
      <p>Перенос сессии не поддерживается.</p>
      <p>Войдите заново на этом зеркале.</p>
      <p class="redirect-text">Перенаправляем на главную...</p>
    </div>
  </div>
</template>

<style scoped lang="sass">
.transfer-page
  max-width: 600px
  margin: $big auto
  padding: $medium

.transfer-message
  text-align: center
  background: $bg-element
  border-radius: $minor
  padding: $big
  box-shadow: 0 2px 8px $shadow-color


p
  color: $text-muted
  margin-bottom: $medium
  line-height: 1.6

.redirect-text
  color: $text-muted
  font-style: italic
</style>
