<template>
  {{ humanDate }}
</template>

<script setup lang="ts">
import { computed } from "vue";
import dayjs from "dayjs";
import { DATE_TIME_FORMAT } from "@/shared/lib/utils/datetime";

const props = withDefaults(defineProps<{ date: string; format?: string }>(), {
  format: DATE_TIME_FORMAT,
});

const humanDate = computed(() => dayjs(props.date).format(props.format));
</script>
