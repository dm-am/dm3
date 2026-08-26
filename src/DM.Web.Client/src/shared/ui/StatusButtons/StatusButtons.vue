<script setup lang="ts" generic="T extends string">
/**
 * StatusButtons — the status-transition action group. Draws the transitions it
 * is given, runs the chosen one through `apply`, and sends a destructive one
 * (danger) through a ConfirmDialog first.
 *
 * The blog and the game had one copy of this each, alike to the letter except
 * for the store they read and the noun in three sentences. What legitimately
 * differs stays with the caller: which transitions the state machine offers
 * (features/*-actions/model/transitions.ts — a blog has no Finish) and what
 * calling one does. Gating (who may change status) is the caller's business
 * too — this component only renders and dispatches.
 *
 * `variant`:
 *  - "strip" (default): sidebar-style plain-link actions (one per line).
 *  - "button": a horizontal group of real buttons for page surfaces.
 */
import { computed, ref, type Ref } from "vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { notifyFailure } from "@/shared/lib/errors";
import type { GeneralError } from "@/shared/api/models/common";
import type { StatusTransitionOption } from "./types";

const props = withDefaults(
  defineProps<{
    /** Transitions to offer, in the order they are drawn. */
    transitions: StatusTransitionOption<T>[];
    /**
     * Runs the chosen transition; resolves to the problem document when the
     * server refused it and to null when it went through.
     */
    apply: (value: T) => Promise<GeneralError | null>;
    /**
     * The module in the genitive case, as the three sentences here spell it:
     * "блога", "игры".
     */
    subject: string;
    variant?: "strip" | "button";
  }>(),
  {
    variant: "strip",
  },
);

// Declared through Ref<…>: ref() unwraps a generic parameter into UnwrapRef<T>,
// and the value read back out then no longer satisfies the T that `apply` asks
// for, though it is the very value that came in.
const pending = ref(null) as Ref<T | null>;
const confirmOption = ref(null) as Ref<{
  value: T;
  label: string;
} | null>;

const confirmMessage = computed(() =>
  confirmOption.value
    ? `Действие "${confirmOption.value.label}" изменит статус ${props.subject}. Продолжить?`
    : "",
);

async function run(t: T) {
  pending.value = t;
  const error = await props.apply(t);
  pending.value = null;
  if (error)
    notifyFailure(error, `Не удалось изменить статус ${props.subject}`);
}

function onClick(t: StatusTransitionOption<T>) {
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
      :title="`Изменение статуса ${subject}`"
      :message="confirmMessage"
      :confirm-label="confirmOption?.label"
      danger
      @confirm="confirm"
      @cancel="confirmOption = null"
    />
  </template>
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
    +button-danger
</style>
