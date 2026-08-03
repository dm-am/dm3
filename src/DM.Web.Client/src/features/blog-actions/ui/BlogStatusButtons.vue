<script setup lang="ts">
/**
 * BlogStatusButtons — the blog status-transition button group. Mirrors
 * GameStatusButtons: reads the current status from the blog-details store,
 * offers the applicable transitions (Start / Close / Reopen), each calling
 * store.transitionStatus. Destructive transitions (Close) go through a
 * ConfirmDialog.
 *
 * Gating (who may change status) is the caller's responsibility — this
 * component only renders and dispatches.
 *
 * `variant`:
 *  - "strip" (default): sidebar-style plain-link actions (one per line).
 *  - "button": a horizontal group of real buttons for page surfaces.
 */
import { computed, ref } from "vue";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore, BlogStatusTransition } from "@/entities/blog";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { availableStatusTransitions } from "../model/transitions";
import { notifyFailure } from "@/shared/lib/errors";

withDefaults(defineProps<{ variant?: "strip" | "button" }>(), {
  variant: "strip",
});

const store = useBlogDetailsStore();
const { blog } = storeToRefs(store);

const transitions = computed(() =>
  availableStatusTransitions(blog.value?.status),
);

const pending = ref<BlogStatusTransition | null>(null);
const confirmOption = ref<{
  value: BlogStatusTransition;
  label: string;
} | null>(null);

const confirmMessage = computed(() =>
  confirmOption.value
    ? `Действие "${confirmOption.value.label}" изменит статус блога. Продолжить?`
    : "",
);

async function run(t: BlogStatusTransition) {
  pending.value = t;
  const error = await store.transitionStatus(t);
  pending.value = null;
  if (error) notifyFailure(error, "Не удалось изменить статус блога");
}

function onClick(t: {
  value: BlogStatusTransition;
  label: string;
  danger?: boolean;
}) {
  if (t.danger) {
    confirmOption.value = { value: t.value, label: t.label };
  } else {
    run(t.value);
  }
}

async function confirm() {
  const opt = confirmOption.value;
  confirmOption.value = null;
  if (opt) await run(opt.value);
}
</script>

<template>
  <template v-if="transitions.length">
    <!-- Sidebar strip variant -->
    <template v-if="variant === 'strip'">
      <li v-for="t in transitions" :key="t.value" class="link">
        <span class="muted" aria-hidden="true">- </span>
        <button
          type="button"
          class="strip-action"
          :class="{ danger: t.danger }"
          :disabled="pending === t.value"
          @click="onClick(t)"
        >
          {{ t.label }}
        </button>
      </li>
    </template>

    <!-- Page button-group variant -->
    <div v-else class="status-buttons">
      <button
        v-for="t in transitions"
        :key="t.value"
        type="button"
        class="status-btn"
        :class="{ danger: t.danger }"
        :disabled="pending === t.value"
        @click="onClick(t)"
      >
        {{ t.label }}
      </button>
    </div>

    <ConfirmDialog
      :show="!!confirmOption"
      title="Изменение статуса блога"
      :message="confirmMessage"
      :confirm-label="confirmOption?.label"
      danger
      @confirm="confirm"
      @cancel="confirmOption = null"
    />
  </template>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

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

  &.danger
    color: $accent-red

  &:disabled
    opacity: 0.6
    cursor: default

.status-buttons
  display: flex
  flex-wrap: wrap
  gap: $small

.status-btn
  font-size: $secondary-font-size
  +button

  &.danger
    background-color: $accent-red
    border-color: $accent-red
</style>
