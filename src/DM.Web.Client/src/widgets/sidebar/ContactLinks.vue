<template>
  <SidebarBlock token="ContactForms">
    <template #title>Обратная связь</template>
    <!-- The "- " prefix is decorative (same idiom as SidebarForwardLink):
         aria-hidden keeps it out of the accessibility tree, and the
         block/inline flow (not flex) keeps the line copying as one line
         instead of "- \n<link text>". The prefix itself is selectable. -->
    <div v-once class="contact-links">
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
    </div>
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

// Plain block line (dash span + link are inline on a shared baseline).
// Deliberately NOT flex: element flex items copy with a newline between
// them, which made the line copy as "- \nОбратиться в поддержку".
.contact-item
  display: block

.muted
  color: $text-muted
</style>
