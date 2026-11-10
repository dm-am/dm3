<script setup lang="ts" generic="T extends string">
// A destructive transition asks through a ConfirmDialog first and paints nothing red: the question says
// the move is heavy. Which transitions there are, and who may run them, is the caller's.
import { computed, ref, type Ref } from "vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { notifyFailure } from "@/shared/lib/errors";
import type { GeneralError } from "@/shared/api/models/common";
import type { StatusTransitionOption } from "./types";
import { OWNS_REFUSAL, type RequestOptions } from "@/shared/api/interceptor";

const props = defineProps<{
  /** Transitions to offer, in the order they are drawn. */
  transitions: StatusTransitionOption<T>[];
  /**
   * Runs the chosen transition; resolves to the problem document when the
   * server refused it and to null when it went through. The flags are the
   * confirmation's, which prints a refusal in itself: passed on to the
   * request, they keep the interceptor from saying it a second time.
   */
  apply: (value: T, options?: RequestOptions) => Promise<GeneralError | null>;
  /**
   * The module in the genitive case, as the sentences here spell it:
   * "блога", "игры".
   */
  subject: string;
}>();

// Through Ref<…>: ref() unwraps a generic into UnwrapRef<T>, which no longer satisfies `apply`.
const pending = ref(null) as Ref<T | null>;
const confirmOption = ref(null) as Ref<{
  value: T;
  label: string;
} | null>;

// The heading is the button's label as a question, the confirm button its verb alone.
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
  const error = await props.apply(opt.value, OWNS_REFUSAL);
  return { error };
}
</script>

<template>
  <template v-if="transitions.length">
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
@use "@/assets/styles/Links" as *

.link
  display: block

// Drawn as every other link of the sidebar menu; a destructive move is not coloured.
.strip-action
  +inline-link-button
</style>
