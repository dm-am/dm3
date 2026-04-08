<template>
  <SidebarBlock token="ContactForms">
    <template #title>Обратная связь</template>
    <div v-once class="contact-links">
      <div class="contact-item">
        <span class="muted">-&nbsp;</span>
        <router-link to="/support">Обратиться в поддержку</router-link>
      </div>
      <div class="contact-item">
        <span class="muted">-&nbsp;</span>
        <router-link to="/complaint">Оставить жалобу</router-link>
      </div>
    </div>
  </SidebarBlock>
</template>

<script setup lang="ts">
import SidebarBlock from "./SidebarBlock.vue";
</script>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.contact-links
  display: flex
  flex-direction: column
  gap: $tiny

.contact-item
  display: flex
  align-items: baseline

.muted
  color: $text-muted
</style>
