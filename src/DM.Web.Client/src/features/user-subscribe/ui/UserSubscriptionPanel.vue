<script setup lang="ts">
// A box saves when pressed; the panel does not close while its request is out.
import { computed, onBeforeUnmount, onMounted, ref, useId } from "vue";
import { storeToRefs } from "pinia";
import { useSubscriptionsStore } from "@/entities/subscription";
import {
  SubscriptionSettings,
  SubscriptionTargetType,
} from "@/shared/api/models/subscriptions";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import Button from "@/shared/ui/Button/Button.vue";
import {
  CATEGORY_BITS,
  USER_SUBSCRIPTION_CATEGORIES,
  useCategoryEvents,
  withCategories,
} from "../model/categories";
import { SUBSCRIBE_FAILED, UNSUBSCRIBE_FAILED } from "../model/refusals";
import { useAnchoredPosition } from "../lib/useAnchoredPosition";

const props = defineProps<{
  userId: string;
  /** A click inside it belongs to the control that opened the panel. */
  anchor: HTMLElement | null;
}>();

const emit = defineEmits<{
  (e: "close", returnFocus: boolean): void;
}>();

const store = useSubscriptionsStore();
const { userSubscriptions } = storeToRefs(store);

const existing = computed(() =>
  userSubscriptions.value.find((s) => s.targetId === props.userId),
);

const panel = ref<HTMLElement | null>(null);
const { style } = useAnchoredPosition(() => props.anchor, panel);

const titleId = useId();
const eventsId = (bit: number) => `${titleId}-events-${bit}`;
const eventsOf = useCategoryEvents();

const busy = ref(false);
const unsubscribing = ref(false);
const pending = ref<number | null>(null);
const saved = ref<number | null>(null);

const result = useActionResult();

const shown = computed(
  () => pending.value ?? (existing.value?.settings ?? 0) & CATEGORY_BITS,
);

const isOn = (bit: number) => (shown.value & bit) !== 0;

// A press while a save is out is refused before it toggles, so it never races the first.
function holdWhileBusy(event: Event) {
  if (busy.value) event.preventDefault();
}

async function toggle(bit: number, on: boolean) {
  const current = existing.value;
  const before = current ? current.settings & CATEGORY_BITS : 0;
  const categories = on ? before | bit : before & ~bit;

  busy.value = true;
  pending.value = categories;
  saved.value = null;
  result.clear();
  let error;
  let fallback: string;
  if (!current) {
    fallback = SUBSCRIBE_FAILED;
    error = await store.subscribe(
      SubscriptionTargetType.User,
      props.userId,
      withCategories(SubscriptionSettings.InApp, categories),
    );
  } else if (categories === 0) {
    fallback = UNSUBSCRIBE_FAILED;
    error = await store.unsubscribe(current.id);
  } else {
    fallback = "Не удалось сохранить подписку";
    error = await store.updateSettings(
      current.id,
      withCategories(current.settings, categories),
    );
  }
  pending.value = null;
  busy.value = false;

  if (error) {
    result.refuse(error, fallback);
    return;
  }
  saved.value = categories === 0 ? null : bit;
}

async function unsubscribe() {
  const current = existing.value;
  if (!current || busy.value) return;
  busy.value = true;
  unsubscribing.value = true;
  saved.value = null;
  result.clear();
  const error = await store.unsubscribe(current.id);
  unsubscribing.value = false;
  busy.value = false;
  if (error) {
    result.refuse(error, UNSUBSCRIBE_FAILED);
    return;
  }
  emit("close", true);
}

function dismiss(returnFocus: boolean) {
  if (busy.value) return;
  emit("close", returnFocus);
}

// A panel closed while its request is out would take the refusal with it.
defineExpose({ busy });

const inside = (target: EventTarget | null) =>
  target instanceof Node &&
  (!!panel.value?.contains(target) || !!props.anchor?.contains(target));

function onDocumentClick(event: MouseEvent) {
  if (!inside(event.target)) dismiss(false);
}

function onKeydown(event: KeyboardEvent) {
  if (event.key !== "Escape") return;
  // Escape is the panel's: a window or a filter under it keeps its own.
  event.stopPropagation();
  dismiss(true);
}

function onFocusOut(event: FocusEvent) {
  // Null when the focus went nowhere in particular, which the click answers.
  if (event.relatedTarget && !inside(event.relatedTarget)) dismiss(false);
}

onMounted(() => {
  // Capture: a press that removes its own node must still count as inside.
  document.addEventListener("click", onDocumentClick, true);
  // The panel takes the focus, not its first box, whose ring read as a box singled out.
  panel.value?.focus({ preventScroll: true });
});

onBeforeUnmount(() => {
  document.removeEventListener("click", onDocumentClick, true);
});
</script>

<template>
  <div
    ref="panel"
    class="subscription-panel"
    role="dialog"
    :aria-labelledby="titleId"
    tabindex="-1"
    :style="style"
    @keydown="onKeydown"
    @focusout="onFocusOut"
  >
    <p :id="titleId" class="panel-title">Присылать уведомления</p>

    <label
      v-for="category in USER_SUBSCRIPTION_CATEGORIES"
      :key="category.bit"
      class="category"
    >
      <input
        type="checkbox"
        :checked="isOn(category.bit)"
        :aria-busy="busy || undefined"
        :aria-describedby="
          eventsOf(category.bit) ? eventsId(category.bit) : undefined
        "
        @click="holdWhileBusy"
        @change="
          toggle(category.bit, ($event.target as HTMLInputElement).checked)
        "
      />
      <span class="category-text">
        <span class="category-line"
          ><span class="category-name">{{ category.label }}</span
          ><template v-if="saved === category.bit"
            ><span class="copy-space">{{ " " }}</span
            ><span class="category-saved">Сохранено</span></template
          ></span
        >
        <span
          v-if="eventsOf(category.bit)"
          :id="eventsId(category.bit)"
          class="category-events"
          >{{ eventsOf(category.bit) }}</span
        >
      </span>
    </label>

    <ActionResult :result="result" class="panel-refusal" />

    <div class="panel-footer">
      <Button
        v-if="existing"
        type="button"
        :loading="unsubscribing"
        @click="unsubscribe"
      >
        Отписаться
      </Button>
      <p v-else class="panel-idle">Не подписан. Отметьте, о чем сообщать.</p>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/ZIndex" as *
@use "@/assets/styles/Filters" as *
@use "@/assets/styles/Inputs" as *

.subscription-panel
  position: fixed
  z-index: $z-dropdown
  +dropdown-surface
  box-sizing: border-box
  width: max-content
  max-width: calc(100vw - #{2 * $small})
  padding: $medium
  outline: none

.panel-title
  margin: 0 0 $small
  +field-caption

.category
  display: flex
  align-items: flex-start
  gap: $small
  cursor: pointer

  & + .category
    margin-top: $small

  input
    flex-shrink: 0
    margin: $tiny 0 0

.category-text,
.category-line,
.category-events
  display: block

.category-name
  font-size: $font-size
  color: $text

.category-saved
  margin-left: $small
  font-size: $secondary-font-size
  color: $text-muted

.category-events
  font-size: $secondary-font-size
  color: $text-muted

.panel-refusal
  width: 0
  min-width: 100%

.panel-footer
  margin: $medium (-$medium) 0
  padding: $medium $medium 0
  border-top: 1px solid $border

.panel-idle
  margin: 0
  font-size: $secondary-font-size
  color: $text-muted
</style>
