<script setup lang="ts">
import { computed } from "vue";
import { DATE_TIME_FORMAT, formatSiteTime } from "@/shared/lib/utils/datetime";

const props = defineProps<{ date: string }>();

// On the site's clock: the account's for a signed-in reader, the device's for
// a guest (siteTime).
const humanDate = computed(() => formatSiteTime(props.date, DATE_TIME_FORMAT));
</script>

<template>
  {{ humanDate }}
</template>
