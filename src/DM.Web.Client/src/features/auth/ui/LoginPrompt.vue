<script setup lang="ts">
// The site-wide guest gate: it stands where the form would be, so it is drawn as a refusal.
import { useRoute } from "vue-router";
import RefusalNote from "@/shared/ui/Layout/RefusalNote.vue";

defineProps<{
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
@use "@/assets/styles/Links" as *

// The spacing is this wrapper's, so a caller can take it away and leave the plaque whole.
.login-prompt
  width: 100%
  padding: $small 0

  a
    +link-tone($link)
</style>
