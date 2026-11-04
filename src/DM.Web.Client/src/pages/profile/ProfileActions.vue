<script setup lang="ts">
/**
 * The row of actions of a profile: the owner's edit and settings, a reader's
 * actions on somebody else. The edit mode saves from ProfileEditBar, so the
 * row stands down while it lasts.
 */
import { computed } from "vue";
import type { User } from "@/shared/api/models/community";
import type { ActionResult } from "@/shared/lib/composables/useActionResult";
import { UserSubscribeButton } from "@/features/user-subscribe";
import Button from "@/shared/ui/Button/Button.vue";
import { ButtonRow } from "@/shared/ui/ButtonRow";
import type { ProfileBlacklist } from "./useProfileBlacklist";

const props = defineProps<{
  user: User;
  /** The name in the address, which the recommendation is written to. */
  username: string;
  canEdit: boolean;
  isEditMode: boolean;
  isOwnProfile: boolean;
  /** A signed-in reader on somebody else's profile. */
  viewerOfOther: boolean;
  /** Whether the server lets the viewer write a recommendation about the owner. */
  canEndorse: boolean;
  blacklist: ProfileBlacklist;
  /** A refused one-press subscribe, under the row of actions. */
  subscribeResult: ActionResult;
}>();

const emit = defineEmits<{
  edit: [];
}>();

const { isBlocked, isBlockLoading, toggleBlock } = props.blacklist;

const writeEndorsementLink = computed(() => ({
  name: "write-endorsement" as const,
  params: { username: props.username },
}));

/** Whether the server says a private message from the viewer would be taken. */
const canMessage = computed(() => props.user.viewer?.canMessage === true);
</script>

<template>
  <!-- Every action stands at every width (ButtonRow). -->
  <ButtonRow v-if="!isEditMode" class="actions">
    <Button v-if="canEdit" @click="emit('edit')">Редактировать</Button>
    <!-- Navigations are links drawn as buttons: a button inside a link is two stops for the keyboard. -->
    <template v-else-if="viewerOfOther">
      <!-- The subscription right after the message, so the recommendation coming and going does not move it. -->
      <router-link
        v-if="canMessage"
        :to="{
          name: 'direct-message',
          params: { username: user.username },
        }"
        class="action-link"
        >Написать сообщение</router-link
      >
      <UserSubscribeButton
        :user-id="user.id"
        :username="user.username"
        :result="subscribeResult"
      />
      <!-- Offered where the server says the recommendation would be taken. -->
      <router-link
        v-if="canEndorse"
        :to="writeEndorsementLink"
        class="action-link"
        >Написать рекомендацию</router-link
      >
      <Button :loading="isBlockLoading" @click="toggleBlock">
        {{ isBlocked ? "Убрать из черного списка" : "В черный список" }}
      </Button>
    </template>
    <!-- The account settings, from the owner's own page only, where a visitor finds the actions. -->
    <router-link
      v-if="isOwnProfile"
      :to="{ name: 'account' }"
      class="action-link"
      >Настройки</router-link
    >
  </ButtonRow>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Buttons" as *

// A row of buttons as wide as the column, in one line (ButtonRow).
.actions
  align-self: stretch
  margin-top: $small

// The secondary button, without the underline of a link.
.action-link
  text-decoration: none
  +button
</style>
