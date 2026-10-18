<template>
  <SidebarBlock token="LegalLinks" title="Правовая информация">
    <!-- The documents of the footer, in the right column of a narrow screen,
         where the footer is a page away. The rows are written as the rows of
         ContactLinks are: the "- " is decorative and kept out of the
         accessibility tree, and the line is a block of inline text rather
         than a flex row, so it copies as one line. -->
    <li v-once class="legal-links">
      <div class="legal-item">
        <span class="muted" aria-hidden="true">-&nbsp;</span>
        <router-link :to="{ name: 'user-agreement' }"
          >Пользовательское соглашение</router-link
        >
      </div>
      <div class="legal-item">
        <span class="muted" aria-hidden="true">-&nbsp;</span>
        <router-link :to="{ name: 'privacy-policy' }"
          >Политика конфиденциальности</router-link
        >
      </div>
    </li>
  </SidebarBlock>
</template>

<script setup lang="ts">
import SidebarBlock from "./SidebarBlock.vue";
</script>

<style scoped lang="sass">
.legal-links
  display: flex
  flex-direction: column
  gap: $tiny

.legal-item
  display: block

.muted
  color: $text-muted
</style>
