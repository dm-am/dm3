<template>
  <SidebarBlock token="ContactForms" title="Обратная связь">
    <!-- The "- " is decorative; inline flow, not flex, so a row copies as one line -->
    <li v-once class="contact-links">
      <div class="contact-item">
        <span class="muted" aria-hidden="true">-&nbsp;</span>
        <router-link :to="{ name: 'support' }"
          >Обратиться в поддержку</router-link
        >
      </div>
      <div class="contact-item">
        <span class="muted" aria-hidden="true">-&nbsp;</span>
        <router-link :to="{ name: 'complaint' }">Оставить жалобу</router-link>
      </div>
    </li>
  </SidebarBlock>
</template>

<script setup lang="ts">
import SidebarBlock from "./SidebarBlock.vue";
</script>

<style scoped lang="sass">
.contact-links
  display: flex
  flex-direction: column
  gap: $tiny

// Not flex: flex items copy with a newline between them.
.contact-item
  display: block
</style>
