<script setup lang="ts" generic="T extends string">
/**
 * StatusButtons — the status-transition action group. Draws the transitions it
 * is given, runs the chosen one through `apply`, and sends a destructive one
 * (danger) through a ConfirmDialog first. The flag decides whether the dialog
 * opens at all and paints nothing: neither the control nor the dialog carries
 * red, the question is what says the move is heavy.
 *
 * A confirmed transition is the dialog's action: the window waits shut for the
 * answer, prints a refusal in itself and closes on success alone. A plain one
 * runs on the spot and a refusal goes to a plate, there being no window to
 * print it in.
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
     * server refused it and to null when it went through. The flags are the
     * confirmation's, which prints a refusal in itself: passed on to the
     * request, they keep the interceptor from saying it a second time.
     */
    apply: (
      value: T,
      options?: { ownsRefusal?: boolean; ownsRateLimit?: boolean },
    ) => Promise<GeneralError | null>;
    /**
     * The module in the genitive case, as the sentences here spell it:
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

// The dialog asks the question in the words of the button that opened it. The
// transition label is a verb and its object ("Закрыть игру"), so the heading is
// that label turned into a question and the confirm button is its first word,
// the verb alone, the length every confirm button on the site keeps.
const confirmTitle = computed(() =>
  confirmOption.value ? `${confirmOption.value.label}?` : "",
);

const confirmVerb = computed(
  () => confirmOption.value?.label.split(" ")[0] ?? "",
);

const confirmMessage = computed(() =>
  confirmOption.value ? `Статус ${props.subject} сменится сразу` : "",
);

/** A refusal the server did not word, in a plate and in the window alike. */
const failure = computed(() => `Не удалось изменить статус ${props.subject}`);

async function run(t: T) {
  pending.value = t;
  const error = await props.apply(t);
  pending.value = null;
  if (error) notifyFailure(error, failure.value);
}

function onClick(t: StatusTransitionOption<T>) {
  if (t.danger) {
    confirmOption.value = { value: t.value, label: t.label };
  } else {
    run(t.value);
  }
}

/** The confirmation's action: the transition the window asked about. */
async function confirmed() {
  const opt = confirmOption.value;
  if (!opt) return;
  const error = await props.apply(opt.value, {
    ownsRefusal: true,
    ownsRateLimit: true,
  });
  return { error };
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
        :disabled="pending === t.value"
        @click="onClick(t)"
      >
        {{ t.label }}
      </button>
    </div>

    <ConfirmDialog
      :show="!!confirmOption"
      :title="confirmTitle"
      :message="confirmMessage"
      :confirm-label="confirmVerb"
      :failure="failure"
      :action="confirmed"
      @update:show="(open: boolean) => !open && (confirmOption = null)"
    />
  </template>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.link
  display: block

.muted
  color: $text-muted

// A word of the sidebar menu, drawn as every other link of it, out of reach
// included. A destructive move is not coloured: it asks first, and the
// question carries the weight.
.strip-action
  +inline-link-button

.status-buttons
  display: flex
  flex-wrap: wrap
  gap: $small

// The shared secondary button, a destructive move included: see above.
.status-btn
  font-size: $secondary-font-size
  +button
</style>
