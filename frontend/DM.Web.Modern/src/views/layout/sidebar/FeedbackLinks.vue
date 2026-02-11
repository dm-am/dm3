<template>
  <menu-block token="Feedback">
    <template #title>Обратная связь</template>
    <div class="feedback-links">
      <div class="feedback-item">
        <span class="muted">-&nbsp;</span>
        <router-link to="/support">Обратиться в поддержку</router-link>
      </div>
      <div class="feedback-item">
        <span class="muted">-&nbsp;</span>
        <router-link to="/complaint">Оставить жалобу</router-link>
      </div>
    </div>
  </menu-block>
</template>

<script setup lang="ts">
import MenuBlock from "@/views/layout/MenuBlock.vue";
</script>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.feedback-links
  display: flex
  flex-direction: column
  gap: $tiny

.feedback-item
  display: flex
  align-items: baseline

.muted
  color: $text-muted
</style>
