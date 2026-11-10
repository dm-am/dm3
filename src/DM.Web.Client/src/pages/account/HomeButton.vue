<script setup lang="ts">
// The one control a used, refused or dead account link leaves on its card.
import { useRouter } from "vue-router";
import Button from "@/shared/ui/Button/Button.vue";

const router = useRouter();
</script>

<template>
  <Button
    class="auth-button auth-button--spaced"
    type="button"
    variant="primary"
    @click="router.push('/')"
  >
    На главную
  </Button>
</template>
