<template>
  {{ humanDate }}
</template>

<script setup lang="ts">
import { computed } from "vue";
import { DATE_TIME_FORMAT, formatSiteTime } from "@/shared/lib/utils/datetime";

const props = withDefaults(defineProps<{ date: string; format?: string }>(), {
  format: DATE_TIME_FORMAT,
});

// On the site's clock: the account's for a signed-in reader, the device's for
// a guest (siteTime).
const humanDate = computed(() => formatSiteTime(props.date, props.format));
</script>
