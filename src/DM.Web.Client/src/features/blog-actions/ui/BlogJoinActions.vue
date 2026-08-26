<script setup lang="ts">
/**
 * BlogJoinActions — the reader-facing "actions with the blog" group:
 * subscribe / unsubscribe (reader role, store.subscribe/unsubscribe).
 * Mirrors GameJoinActions; blogs have no "apply to join" — the only reader
 * action is the subscription toggle ("Добавить в читаемое" per the doc).
 *
 * Gating (only shown for an authenticated non-owner) is the caller's
 * responsibility. `variant` mirrors BlogStatusButtons ("strip" for the
 * sidebar, "button" for page surfaces).
 *
 * isSubscribed derives from the readers slice (blogs serve no participation
 * flags), so the readers list is loaded on mount if not present yet.
 */
import { onMounted, ref } from "vue";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore } from "@/entities/blog";
import { notifyFailure } from "@/shared/lib/errors";

withDefaults(defineProps<{ variant?: "strip" | "button" }>(), {
  variant: "strip",
});

const store = useBlogDetailsStore();
const { blog, isSubscribed, readers, readersLoading } = storeToRefs(store);

onMounted(() => {
  if (blog.value && !readers.value.length && !readersLoading.value) {
    store.loadReaders(blog.value.id);
  }
});

const busy = ref(false);

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
  <!-- Sidebar strip variant -->
  <template v-if="variant === 'strip'">
    <li class="link">
      <span class="muted" aria-hidden="true">- </span>
      <button
        type="button"
        class="strip-action"
        :disabled="busy"
        @click="toggleSubscribe"
      >
        {{ isSubscribed ? "Убрать из читаемого" : "Добавить в читаемое" }}
      </button>
    </li>
  </template>

  <!-- Page button-group variant -->
  <div v-else class="join-buttons">
    <button
      type="button"
      class="join-btn"
      :disabled="busy"
      @click="toggleSubscribe"
    >
      {{ isSubscribed ? "Убрать из читаемого" : "Добавить в читаемое" }}
    </button>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.link
  display: block

.muted
  color: $text-muted

.strip-action
  padding: 0
  border: none
  background: none
  font: inherit
  color: $link
  cursor: pointer

  &:hover:not(:disabled)
    color: $link-hover
    text-decoration: underline

  &:disabled
    opacity: 0.6
    cursor: default

.join-buttons
  display: flex
  flex-wrap: wrap
  gap: $small

.join-btn
  font-size: $secondary-font-size
  +button
</style>
