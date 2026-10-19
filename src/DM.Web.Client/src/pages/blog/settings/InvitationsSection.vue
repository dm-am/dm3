<script setup lang="ts">
/**
 * The blog's outstanding invitations, inviting a reader and cancelling one;
 * assistant invites are RolesSection's. The read, the invite and the cancel
 * ask three intentions, so the page passes two flags. A sent invitation says
 * nothing in the corner: its row stands right above the control.
 */
import { computed, onMounted, ref } from "vue";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore, blogApi } from "@/entities/blog";
import type { BlogInvitation } from "@/entities/blog";
import { INVITATION_TYPE_LABELS } from "@/entities/game";
import { UserLink } from "@/entities/user";
import { UserAutocomplete } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import { SettingsSection } from "@/shared/ui/SettingsSection";
import { RemoveButton } from "@/shared/ui/RemoveButton";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { refusalHere } from "@/shared/lib/errors";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { ErrorState } from "@/shared/ui/ErrorState";
import { shownOnPage } from "@/shared/lib/composables/useGuardedRequest";

defineProps<{
  canInvite: boolean;
  canCancel: boolean;
}>();

const store = useBlogDetailsStore();
const { blog } = storeToRefs(store);

const invitations = ref<BlogInvitation[]>([]);
/** The list has been read once: until then there is nothing to keep or to call empty. */
const answered = ref(false);
const loadFailed = ref(false);
const LOAD_FAILED = "Не удалось загрузить приглашения";

const username = ref("");
const inviting = ref(false);

/** Oldest first, so the invitation just sent is the row next to the control; the endpoint serves newest first. */
const sentAt = (invitation: BlogInvitation) =>
  new Date(invitation.createdUtc).getTime();

const sorted = computed(() =>
  [...invitations.value].sort((a, b) => sentAt(a) - sentAt(b)),
);

const sentLabel = (invitation: BlogInvitation) =>
  `отправлено ${formatDateFull(invitation.createdUtc)}`;

const blogId = computed(() => blog.value?.id ?? "");

async function load() {
  if (!blogId.value) return;
  const answer = await blogApi.getInvitations(blogId.value);
  if (answer.error) {
    // A failed reload leaves the list alone, so a blip keeps a sent invitation;
    // a failed first read says so in the list's place.
    if (!answered.value) {
      shownOnPage(answer);
      loadFailed.value = true;
    }
    return;
  }
  answered.value = true;
  loadFailed.value = false;
  invitations.value = answer.data?.resources ?? [];
}

onMounted(load);

/**
 * The server kills the live invitation of the same kind to the same person
 * before issuing this one, so a second invitation replaces the row. Taken from
 * the answer, the row survives a failed reload.
 */
function remember(invitation: BlogInvitation) {
  invitations.value = [
    ...invitations.value.filter(
      (i) =>
        !(
          i.invitedUser.username === invitation.invitedUser.username &&
          i.type === invitation.type
        ),
    ),
    invitation,
  ];
}

const inviteResult = useActionResult();

const cancelRefusal = ref<{ id: string; text: string } | null>(null);

async function invite() {
  if (!blogId.value || !username.value.trim()) return;
  inviting.value = true;
  inviteResult.clear();
  const { data, error } = await blogApi.inviteReader(
    blogId.value,
    username.value.trim(),
  );
  inviting.value = false;
  if (error) {
    inviteResult.refuse(error, "Не удалось отправить приглашение");
    return;
  }
  if (data) remember(data);
  username.value = "";
  await load();
}

async function cancel(invitation: BlogInvitation) {
  if (!blogId.value) return;
  cancelRefusal.value = null;
  const { error } = await blogApi.cancelInvitation(blogId.value, invitation.id);
  if (error) {
    const text = refusalHere(error, "Не удалось отменить приглашение");
    if (text) cancelRefusal.value = { id: invitation.id, text };
    return;
  }
  await load();
}
</script>

<template>
  <SettingsSection title="Приглашения">
    <ul v-if="sorted.length" class="inv-list">
      <li v-for="inv in sorted" :key="inv.id" class="inv-item">
        <div class="inv-row">
          <UserLink :user="inv.invitedUser" hide-badge />
          <span class="inv-type">{{ INVITATION_TYPE_LABELS[inv.type] }}</span>
          <span class="inv-sent">{{ sentLabel(inv) }}</span>
          <RemoveButton v-if="canCancel" @click="cancel(inv)">
            Отменить
          </RemoveButton>
        </div>
        <ActionResult
          v-if="cancelRefusal?.id === inv.id"
          :text="cancelRefusal.text"
          class="inv-refusal"
        />
      </li>
    </ul>
    <div
      v-else-if="!answered && !loadFailed"
      class="inv-waiting"
      aria-hidden="true"
    />
    <ErrorState v-else-if="!answered" :message="LOAD_FAILED" :retry="load" />
    <SecondaryText v-else>Активных приглашений нет</SecondaryText>

    <div v-if="canInvite" class="invite-row">
      <UserAutocomplete
        v-model="username"
        class="invite-row__who"
        placeholder="Имя пользователя"
      />
      <Button
        type="button"
        variant="primary"
        :loading="inviting"
        :disabled="!username.trim()"
        @click="invite"
      >
        Пригласить читателя
      </Button>
    </div>
    <ActionResult v-if="canInvite" :result="inviteResult" />
  </SettingsSection>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *

.inv-list
  list-style: none
  display: flex
  flex-direction: column
  gap: $tiny
  margin-bottom: $small

// The line of "Активных приглашений нет", the answer most sections get.
.inv-waiting
  width: 40%
  height: 1.3 * $secondary-font-size
  +skeleton-shimmer

.inv-row
  display: flex
  align-items: center
  gap: $small

.inv-refusal
  --action-result-margin: #{$tiny} 0 0

.inv-type,
.inv-sent
  color: $text-muted
  font-size: $secondary-font-size

// The field takes what the button leaves, down to 80.
.invite-row
  display: flex
  align-items: flex-start
  gap: $small

.invite-row__who
  flex: 1 1 0
  min-width: $grid-step * 20
</style>
