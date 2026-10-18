<script setup lang="ts">
/**
 * UserSubscribeButton — the subscription to a person, in the row of actions of
 * their profile.
 *
 * "Подписаться" subscribes in one press to all three kinds of news, as the
 * subscription to a game does, and the button turns into "Подписан" with the
 * arrow of a dropdown: the answer is the word the reader pressed. It used to
 * open a form with a second "Подписаться" in it, which made the one thing a
 * reader came for take two presses and a decision they had not asked for.
 *
 * "Подписан" opens UserSubscriptionPanel, where the kinds are changed one box
 * at a time and the subscription is ended. Pressed again, it closes the panel,
 * whatever the word on it has become meanwhile: clearing every box in the
 * panel ends the subscription and the button reads "Подписаться" over the
 * still open panel, and a press there is taken as leaving, not as a second
 * subscribe.
 *
 * A refused one-press subscribe is said in `result`, the refusal line the row
 * of actions keeps under itself: the button is one item of a row of buttons
 * (shared/ui/ButtonRow), and a line under it inside that row would stand
 * between the buttons.
 * The panel hangs from the button's own box at every width: the row never
 * puts the button away, so there is nothing else to hang from.
 */
import { computed, ref } from "vue";
import { storeToRefs } from "pinia";
import { useSubscriptionsStore } from "@/entities/subscription";
import {
  SubscriptionSettings,
  SubscriptionTargetType,
} from "@/shared/api/models/subscriptions";
import type { ActionResult } from "@/shared/lib/composables/useActionResult";
import Button from "@/shared/ui/Button/Button.vue";
import { SvgIcon } from "@/shared/ui/Icon";
import UserSubscriptionPanel from "./UserSubscriptionPanel.vue";

const props = defineProps<{
  userId: string;
  username: string;
  /** Where a refused press is said: the line under the row of actions. */
  result: ActionResult;
}>();

const store = useSubscriptionsStore();
const { userSubscriptions } = storeToRefs(store);

const isSubscribed = computed(() =>
  userSubscriptions.value.some((s) => s.targetId === props.userId),
);

const root = ref<HTMLElement | null>(null);
const panel = ref<InstanceType<typeof UserSubscriptionPanel> | null>(null);
const open = ref(false);
const subscribing = ref(false);

/** The control opens a panel whenever it is "Подписан" or the panel is up. */
const opensPanel = computed(() => isSubscribed.value || open.value);

async function press() {
  if (open.value) {
    // Not while the panel's request is out: its refusal is said in it.
    if (!panel.value?.busy) open.value = false;
    return;
  }
  if (isSubscribed.value) {
    open.value = true;
    return;
  }
  subscribing.value = true;
  props.result.clear();
  const error = await store.subscribe(
    SubscriptionTargetType.User,
    props.userId,
    SubscriptionSettings.UserSubscriptionDefault,
  );
  subscribing.value = false;
  if (error) {
    props.result.refuse(error, "Не удалось подписаться на пользователя");
  }
}

function close(returnFocus: boolean) {
  open.value = false;
  if (returnFocus) root.value?.querySelector("button")?.focus();
}
</script>

<template>
  <span ref="root" class="user-subscribe">
    <Button
      type="button"
      :class="{ active: open }"
      :loading="subscribing"
      :aria-haspopup="opensPanel ? 'dialog' : undefined"
      :aria-expanded="opensPanel ? open : undefined"
      @click="press"
    >
      {{ isSubscribed ? "Подписан" : "Подписаться"
      }}<SvgIcon
        v-if="isSubscribed"
        name="chevronDown"
        class="user-subscribe-chevron"
        aria-hidden="true"
      />
    </Button>
    <UserSubscriptionPanel
      v-if="open"
      ref="panel"
      :user-id="userId"
      :anchor="root"
      @close="close"
    />
  </span>
</template>

<style scoped lang="sass">
.user-subscribe
  display: inline-flex
  flex-shrink: 0

// The arrow of the site's dropdowns (the field of a list, the calendar's
// month): it says a panel is behind the word.
.user-subscribe-chevron
  width: 14px
  height: 14px
  margin-left: $small
  color: $text-muted
</style>
