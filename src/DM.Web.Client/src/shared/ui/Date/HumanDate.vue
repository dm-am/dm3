<template>
  {{ humanDate }}
</template>

<script setup lang="ts">
import { computed } from "vue";
import dayjs from "dayjs";

const props = withDefaults(defineProps<{ date: string; format?: string }>(), {
  format: "DD.MM.YYYY HH:mm",
});

const humanDate = computed(() => dayjs(props.date).format(props.format));
</script>
