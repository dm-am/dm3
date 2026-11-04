<script setup lang="ts">
// The way back from a page to the one it belongs to, above its content.
import type { RouteLocationRaw } from "vue-router";
import { SvgIcon } from "@/shared/ui/Icon";

defineProps<{
  to: RouteLocationRaw;
  label: string;
}>();
</script>

<template>
  <router-link :to="to" class="back-link">
    <SvgIcon name="chevronLeft" />
    {{ label }}
  </router-link>
</template>

<style scoped lang="sass">
.back-link
  display: inline-flex
  align-items: center
  gap: $tiny
  margin-bottom: $medium
</style>
