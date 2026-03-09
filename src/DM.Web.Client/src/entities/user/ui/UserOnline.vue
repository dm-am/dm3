<script setup lang="ts">
import HumanTimespan from "@/shared/ui/Date/HumanTimespan.vue";
import type { User } from "../model/types";
import { computed } from "vue";
import dayjs from "dayjs";

const props = defineProps<{ detailed: boolean; user: User }>();
const online = computed(() => {
  const lastActivityUtc = props.user.lastActivityUtc;
  if (!lastActivityUtc) return false;
  return dayjs().diff(lastActivityUtc, "m", true) < 5;
});
</script>

<template>
  <span :class="{ online }">
    <template v-if="online">online</template>
    <human-timespan v-else-if="detailed" :date="user.lastActivityUtc" />
    <span v-else class="offline">offline</span>
  </span>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.online
  color: $accent-green

.offline
  color: $text-muted
</style>
