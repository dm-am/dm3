<script setup lang="ts">
/**
 * LoginPrompt — the site-wide guest CTA: "Войдите, чтобы <do X>".
 * Single source of truth for every login gate (chat footers, comment
 * forms, create pages, ...): left-aligned secondary text with the
 * "Войдите" link in the regular $link color, opening the login dialog
 * via the ?action=login query (handled globally by the header's
 * GuestActions; the current route and query are preserved).
 */
import { useRoute } from "vue-router";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";

defineProps<{
  /** The goal after "Войдите, чтобы ..." (e.g. "отправлять сообщения"). */
  action: string;
}>();

const route = useRoute();
</script>

<template>
  <SecondaryText class="login-prompt">
    <router-link :to="{ query: { ...route.query, action: 'login' } }"
      >Войдите</router-link
    >, чтобы {{ action }}
  </SecondaryText>
</template>

<style scoped lang="sass">
.login-prompt
  padding: $small

  a
    color: $link

    &:hover
      text-decoration: underline
</style>
