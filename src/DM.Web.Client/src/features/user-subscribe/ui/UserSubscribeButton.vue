<script setup lang="ts">
/**
 * UserSubscribeButton — anchored subscribe control for a user profile.
 *
 * Click on the button opens a popover with three category toggles
 * ("Игры" / "Блоги" / "Топики") bound to the bits of
 * `SubscriptionSettings.Author{Game,Blog,Topic}Events`. Confirming the
 * popover either creates a new subscription with the chosen settings,
 * or updates an existing one's settings — depending on whether the
 * viewer is already subscribed. The popover also provides an explicit
 * "Отписаться" action when the user is subscribed.
 *
 * The component is self-contained: it owns its open/close state, draft
 * settings, focus-trap, click-outside dismissal and ESC dismissal.
 * Callers wire it into a profile with `:userId` + `:username` props.
 */
import { computed, nextTick, ref, watch } from "vue";
import { storeToRefs } from "pinia";
import { useSubscriptionsStore } from "@/entities/subscription";
import {
  SubscriptionSettings,
  SubscriptionTargetType,
} from "@/shared/api/models/subscriptions";
import { useToast } from "@/shared/lib/composables/useToast";
import { notifyFailure } from "@/shared/lib/errors";
import { vClickOutside } from "@/shared/directives/clickOutside";
import Button from "@/shared/ui/Button/Button.vue";

const props = defineProps<{
  userId: string;
  username: string;
}>();

const store = useSubscriptionsStore();
const { userSubscriptions } = storeToRefs(store);
const toast = useToast();

// SSOT: the popover form lives in a single object keyed by the three
// author-category bits. Bit-level enum stays the single source for the
// flag identity — never duplicated as separate booleans elsewhere.
type Draft = {
  games: boolean;
  blogs: boolean;
  topics: boolean;
};

const FLAG_BIT: Record<keyof Draft, SubscriptionSettings> = {
  games: SubscriptionSettings.AuthorGameEvents,
  blogs: SubscriptionSettings.AuthorBlogEvents,
  topics: SubscriptionSettings.AuthorTopicEvents,
};

const DEFAULT_DRAFT: Draft = { games: true, blogs: true, topics: true };

const open = ref(false);
const submitting = ref(false);
const draft = ref<Draft>({ ...DEFAULT_DRAFT });
const popoverRef = ref<HTMLElement | null>(null);

const existing = computed(() =>
  userSubscriptions.value.find((s) => s.targetId === props.userId),
);
const isSubscribed = computed(() => !!existing.value);

function settingsToDraft(settings: number): Draft {
  return {
    games: (settings & FLAG_BIT.games) !== 0,
    blogs: (settings & FLAG_BIT.blogs) !== 0,
    topics: (settings & FLAG_BIT.topics) !== 0,
  };
}

function draftToSettings(d: Draft): SubscriptionSettings {
  // Always keep the InApp channel — users opt in/out of categories,
  // not delivery channel. Email channel is managed separately on the
  // /subscriptions page.
  let s = SubscriptionSettings.InApp;
  if (d.games) s |= FLAG_BIT.games;
  if (d.blogs) s |= FLAG_BIT.blogs;
  if (d.topics) s |= FLAG_BIT.topics;
  return s;
}

// Reset the draft whenever the popover opens — show the current state
// for existing subscriptions, the default ("all on") for new ones.
function openPopover() {
  draft.value = existing.value
    ? settingsToDraft(existing.value.settings)
    : { ...DEFAULT_DRAFT };
  open.value = true;
  nextTick(() => popoverRef.value?.focus());
}

function closePopover() {
  open.value = false;
}

function onEscape(event: KeyboardEvent) {
  if (event.key === "Escape" && open.value) {
    closePopover();
  }
}

// Keep ESC scoped to the popover lifecycle — attach when opened,
// detach when closed. Avoids global key handlers leaking when the
// component is unmounted mid-flight.
watch(open, (isOpen) => {
  if (isOpen) {
    document.addEventListener("keydown", onEscape);
  } else {
    document.removeEventListener("keydown", onEscape);
  }
});

const atLeastOneSelected = computed(
  () => draft.value.games || draft.value.blogs || draft.value.topics,
);

async function confirm() {
  if (!atLeastOneSelected.value) return;
  submitting.value = true;
  try {
    const settings = draftToSettings(draft.value);
    const updating = existing.value;
    const error = updating
      ? await store.updateSettings(updating.id, settings)
      : await store.subscribe(
          SubscriptionTargetType.User,
          props.userId,
          settings,
        );
    if (error) {
      notifyFailure(error, "Не удалось сохранить подписку");
      return;
    }
    toast.success(
      updating
        ? `Подписка на ${props.username} обновлена`
        : `Вы подписались на ${props.username}`,
    );
    closePopover();
  } finally {
    submitting.value = false;
  }
}

async function unsubscribe() {
  if (!existing.value) return;
  submitting.value = true;
  try {
    const error = await store.unsubscribe(existing.value.id);
    if (error) {
      notifyFailure(error, "Не удалось отписаться");
      return;
    }
    toast.success(`Вы отписались от ${props.username}`);
    closePopover();
  } finally {
    submitting.value = false;
  }
}
</script>

<template>
  <div class="user-subscribe">
    <Button
      :disabled="submitting"
      @click="open ? closePopover() : openPopover()"
    >
      {{ isSubscribed ? "Подписан" : "Подписаться" }}
    </Button>

    <div
      v-if="open"
      ref="popoverRef"
      v-click-outside="closePopover"
      class="popover"
      role="dialog"
      aria-label="Настройки подписки"
      tabindex="-1"
    >
      <div class="popover-title">Уведомлять о</div>
      <label class="row">
        <input v-model="draft.games" type="checkbox" :disabled="submitting" />
        <span>Игры</span>
      </label>
      <label class="row">
        <input v-model="draft.blogs" type="checkbox" :disabled="submitting" />
        <span>Блоги</span>
      </label>
      <label class="row">
        <input v-model="draft.topics" type="checkbox" :disabled="submitting" />
        <span>Топики</span>
      </label>
      <p v-if="!atLeastOneSelected" class="hint">
        Выберите хотя бы одну категорию
      </p>
      <div class="actions">
        <Button v-if="isSubscribed" :disabled="submitting" @click="unsubscribe">
          Отписаться
        </Button>
        <span v-if="isSubscribed" class="spacer" />
        <Button :disabled="submitting || !atLeastOneSelected" @click="confirm">
          {{ isSubscribed ? "Сохранить" : "Подписаться" }}
        </Button>
        <Button :disabled="submitting" @click="closePopover"> Отмена </Button>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/_ZIndex"

.user-subscribe
  position: relative
  display: inline-block

.popover
  position: absolute
  top: calc(100% + #{$tiny})
  left: 0
  z-index: $z-popover
  min-width: 220px
  padding: $small $medium
  background-color: $bg-element
  border: 1px solid $border
  border-radius: $border-radius
  box-shadow: 0 4px 16px $shadow-color
  outline: none

.popover-title
  font-size: $secondary-font-size
  color: $text-muted
  margin-bottom: $tiny

.row
  display: flex
  align-items: center
  gap: $small
  padding: $tiny 0
  cursor: pointer
  font-size: $font-size
  color: $text

  input
    cursor: pointer

.hint
  margin: $tiny 0
  font-size: $secondary-font-size
  color: $accent-red

.actions
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small
  margin-top: $small

  .spacer
    flex: 1 1 auto
</style>
