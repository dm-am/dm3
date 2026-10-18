<script setup lang="ts">
/**
 * UserSubscriptionPanel — what a subscription to a person sends, changed in
 * place. It drops from "Подписан" in the profile and from "Настроить" in the
 * list of subscriptions, and it is the same panel in both.
 *
 * A box saves the moment it is pressed, as the boxes of the account settings
 * do, and there is no "Сохранить" and no "Отмена": three boxes did not need a
 * form, and the form cost a second press on every visit. While a save is out
 * the boxes stay drawn as they are and a press on them is held
 * (savingCheckboxes.spec.ts in the account settings says why they are not
 * disabled). The server's answer is the box itself and a grey "Сохранено"
 * beside the one just pressed, which stays until another box is pressed or
 * the panel closes. A refusal puts the box back and says why under the list.
 *
 * The boxes are the subscription. Clearing the last one ends it, so there is
 * no "choose at least one" to tell the reader off with: the panel stays open,
 * says the reader is not subscribed, and ticking any box subscribes again with
 * that box alone. "Отписаться" at the foot is the same thing in one press, and
 * it closes the panel, since the word on the anchor then says it all.
 *
 * The panel closes on a click beside it, on Escape (which gives the focus back
 * to the control it opened from) and when the focus leaves it; not while its
 * request is out, so a refusal always has the panel to be said in.
 */
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
  withCategories,
} from "../model/categories";
import { useAnchoredPosition } from "../lib/useAnchoredPosition";

const props = defineProps<{
  userId: string;
  /**
   * What the panel hangs from. Its position is read off this element, and a
   * click inside it belongs to the control that opened the panel (which
   * closes it on a second press), not to "beside the panel".
   */
  anchor: HTMLElement | null;
}>();

const emit = defineEmits<{
  /** `returnFocus`: the reader left with the keyboard, give the focus back. */
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

/** A request of the panel is out: presses on it are held. */
const busy = ref(false);
/** The request out is "Отписаться": its button wears the wait. */
const unsubscribing = ref(false);
/** The boxes as pressed, drawn while their save is out. */
const pending = ref<number | null>(null);
/** The category the server has just confirmed, marked "Сохранено". */
const saved = ref<number | null>(null);

/** A refusal, under the list. */
const result = useActionResult();

const shown = computed(
  () => pending.value ?? (existing.value?.settings ?? 0) & CATEGORY_BITS,
);

const isOn = (bit: number) => (shown.value & bit) !== 0;

// A box stays drawn as it is while a save is out; a press on it is refused
// before it toggles anything, so it never becomes a second request racing the
// first (the account settings' idiom, savingCheckboxes.spec.ts).
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
    fallback = "Не удалось подписаться на пользователя";
    error = await store.subscribe(
      SubscriptionTargetType.User,
      props.userId,
      withCategories(SubscriptionSettings.InApp, categories),
    );
  } else if (categories === 0) {
    // The last box cleared: the subscription has nothing left to send.
    fallback = "Не удалось отписаться от пользователя";
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
    // The box goes back to what the server holds, and the line says why.
    result.refuse(error, fallback);
    return;
  }
  // An emptied subscription is said by the foot of the panel instead.
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
    result.refuse(error, "Не удалось отписаться от пользователя");
    return;
  }
  // The word on the control the panel hangs from is the answer now.
  emit("close", true);
}

function dismiss(returnFocus: boolean) {
  if (busy.value) return;
  emit("close", returnFocus);
}

// The control the panel hangs from closes it on a second press, and asks this
// first: a panel closed while its request is out takes the refusal with it.
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
  // Null when the focus went nowhere in particular (a click on the page),
  // which the click itself answers.
  if (event.relatedTarget && !inside(event.relatedTarget)) dismiss(false);
}

onMounted(() => {
  // Capture: a press inside that removes its own node (a box whose row is
  // redrawn) must still count as inside when this runs.
  document.addEventListener("click", onDocumentClick, true);
  // The panel takes the focus itself and not its first box: a box with the
  // focus wears the ring of a field, which read as a box singled out. Tab
  // goes on from here into the boxes, and Escape works at once.
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
        :aria-describedby="eventsId(category.bit)"
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
        <span :id="eventsId(category.bit)" class="category-events">{{
          category.events
        }}</span>
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

// The site's floating panel (=dropdown-surface: the ground, frame, corners and
// shadow of the filter dropdowns and the calendar), standing at the fixed
// coordinates useAnchoredPosition reads off the anchor. As wide as its longest
// line, and never wider than the window it is pushed back into.
.subscription-panel
  position: fixed
  z-index: $z-dropdown
  +dropdown-surface
  box-sizing: border-box
  width: max-content
  max-width: calc(100vw - #{2 * $small})
  padding: $medium
  // It holds the focus only to hand it on; the boxes inside show theirs.
  outline: none

// The caption of a group of fields: 14, bold, the colour of the text.
.panel-title
  margin: 0 0 $small
  +field-caption

// A box and its words: the caption in the 16 of a checkbox's caption, what it
// sends under it in the secondary size, both aligned past the box.
.category
  display: flex
  align-items: flex-start
  gap: $small
  cursor: pointer

  & + .category
    margin-top: $small

  input
    flex-shrink: 0
    // The 16 of the box against the first line of the 16 of the caption.
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

// The line wraps inside the panel instead of widening it.
.panel-refusal
  width: 0
  min-width: 100%

// Under a hairline across the whole panel, as a panel's last block is.
.panel-footer
  margin: $medium (-$medium) 0
  padding: $medium $medium 0
  border-top: 1px solid $border

.panel-idle
  margin: 0
  font-size: $secondary-font-size
  color: $text-muted
</style>
