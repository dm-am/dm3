<script setup lang="ts">
/**
 * LoginPrompt — the site-wide guest CTA: "Войдите, чтобы <do X>".
 * Single source of truth for every login gate (chat footers, comment
 * forms, create pages, ...). It stands where the form would be, so it is a
 * refusal and is drawn as one: the RefusalNote plaque, not the grey of an
 * empty-state line. The "Войдите" link is in the regular $link color and
 * opens the login dialog via the ?action=login query (handled globally by
 * the header's GuestActions; the current route and query are preserved).
 */
import { useRoute } from "vue-router";
import RefusalNote from "@/shared/ui/Layout/RefusalNote.vue";

defineProps<{
  /** The goal after "Войдите, чтобы ..." (e.g. "отправлять сообщения"). */
  action: string;
}>();

const route = useRoute();
</script>

<template>
  <div class="login-prompt">
    <RefusalNote>
      <router-link :to="{ query: { ...route.query, action: 'login' } }"
        >Войдите</router-link
      >, чтобы {{ action }}
    </RefusalNote>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

// The plaque's frame starts at the left edge of the column it stands in, as
// the form it replaces would. The 8 above and below is where it stands among
// the other blocks of a page (under the feed of a chat, at the end of the
// comments, alone in place of a page a guest may not use). That spacing is
// this wrapper's and the plaque's padding is its own, so a caller that takes
// the spacing away (a line among the lines of a note) leaves the plaque whole.
// Across the column in a flex column that starts its items at the left too.
.login-prompt
  width: 100%
  padding: $small 0

  // In the link colour inside the plaque, and answering the pointer as every
  // link of the site does.
  a
    +link-tone($link)
</style>
