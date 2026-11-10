<script setup lang="ts">
// A refused subscribe goes to the row's refusal line: a line inside the row would stand between buttons.
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
import { useCategoryEvents } from "../model/categories";
import { SUBSCRIBE_FAILED } from "../model/refusals";
import UserSubscriptionPanel from "./UserSubscriptionPanel.vue";

const props = defineProps<{
  userId: string;
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

const opensPanel = computed(() => isSubscribed.value || open.value);

// Asked with the button, so the panel opens with the words under its boxes.
useCategoryEvents();

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
    props.result.refuse(error, SUBSCRIBE_FAILED);
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

.user-subscribe-chevron
  width: $control-glyph-size
  height: $control-glyph-size
  margin-left: $small
  color: $text-muted
</style>
