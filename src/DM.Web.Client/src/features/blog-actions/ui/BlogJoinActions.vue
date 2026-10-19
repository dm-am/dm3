<script setup lang="ts">
// isSubscribed comes from the readers slice (blogs serve no participation flags).
import { computed, onMounted, ref } from "vue";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore } from "@/entities/blog";
import { notifyFailure } from "@/shared/lib/errors";

const store = useBlogDetailsStore();
const { blog, isSubscribed, readers, readersLoading } = storeToRefs(store);

onMounted(() => {
  if (blog.value && !readers.value.length && !readersLoading.value) {
    store.loadReaders(blog.value.id);
  }
});

const busy = ref(false);

const label = computed(() =>
  isSubscribed.value ? "Убрать из читаемого" : "Добавить в читаемое",
);

async function toggleSubscribe() {
  busy.value = true;
  const error = isSubscribed.value
    ? await store.unsubscribe()
    : await store.subscribe();
  busy.value = false;
  if (error) notifyFailure(error, "Не удалось изменить подписку");
}
</script>

<template>
  <li class="link">
    <span class="muted" aria-hidden="true">- </span>
    <button
      type="button"
      class="strip-action"
      :disabled="busy"
      @click="toggleSubscribe"
    >
      {{ label }}
    </button>
  </li>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

.link
  display: block

.strip-action
  +inline-link-button
</style>
