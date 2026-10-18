<script setup lang="ts">
/**
 * InvitationsSection — outstanding invitations for the blog plus the control
 * to invite a reader and to cancel a pending invitation. Assistant invites
 * live in RolesSection (mirrors the game split: staff invites with roles,
 * audience invites here). Backed by the invitation blogApi; the list is held
 * locally (not part of the shared blog store).
 *
 * Three intentions meet here, so the host page passes two flags rather than
 * one: the list read asks BlogIntention.EditSettings (owner, assistants,
 * senior moderation), inviting a reader asks InviteReader (owner and
 * assistants), and cancelling asks CancelInvitation (the owner alone).
 *
 * Sending an invitation says nothing in the corner: the row for it is in the
 * list right above the control, carrying the moment it was sent. That the row
 * is there at all takes three things, and each of them is a way the list used
 * to lose it - see `sorted`, `load` and `invite` below.
 */
import { computed, onMounted, ref } from "vue";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore, blogApi } from "@/entities/blog";
import type { BlogInvitation } from "@/entities/blog";
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

defineProps<{
  /** Whether the viewer may invite a reader. */
  canInvite: boolean;
  /** Whether the viewer may cancel a pending invitation. */
  canCancel: boolean;
}>();

const store = useBlogDetailsStore();
const { blog } = storeToRefs(store);

const invitations = ref<BlogInvitation[]>([]);
const loading = ref(false);

const username = ref("");
const inviting = ref(false);

const typeLabels: Record<string, string> = {
  reader: "Читатель",
  assistant: "Ассистент",
};

/**
 * Oldest first, so the invitation just sent is the last row - the one next to
 * the control that sent it.
 *
 * The order is the section's own: the endpoint hands the blog's invitations
 * over newest first, its game twin hands them over in no order at all, and
 * neither of those puts the new row where the reader is looking.
 */
const sentAt = (invitation: BlogInvitation) =>
  new Date(invitation.createdUtc).getTime();

const sorted = computed(() =>
  [...invitations.value].sort((a, b) => sentAt(a) - sentAt(b)),
);

/** When the invitation went out, in the row that stands for it. */
const sentLabel = (invitation: BlogInvitation) =>
  `отправлено ${formatDateFull(invitation.createdUtc)}`;

const blogId = computed(() => blog.value?.id ?? "");

async function load() {
  if (!blogId.value) return;
  loading.value = true;
  const { data, error } = await blogApi.getInvitations(blogId.value);
  loading.value = false;
  // A read that failed leaves the list alone. Emptying it on an error meant
  // that a blip in the reload below answered a freshly sent invitation with
  // "Активных приглашений нет".
  if (error) return;
  invitations.value = data?.resources ?? [];
}

onMounted(load);

/**
 * The invitation the server has just answered with, put into the list in
 * place of the one it replaced.
 *
 * The server kills the live invitation of the same kind to the same person
 * before issuing this one (BlogInvitationService.CreateInvitation), so
 * inviting somebody a second time is a replacement, not an addition: what
 * changes in the row is the moment it was sent. Taking the row from the
 * answer also means the reload that follows is free to fail.
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

/** A refused invitation, under the row it was typed in, the name kept. */
const inviteResult = useActionResult();

/** The last refused "Отменить", under the row of the invitation it was about. */
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
          <UserLink :user="inv.invitedUser" />
          <span class="inv-type">{{ typeLabels[inv.type] ?? inv.type }}</span>
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
    <SecondaryText v-else-if="!loading">Активных приглашений нет</SecondaryText>

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
.inv-list
  list-style: none
  display: flex
  flex-direction: column
  gap: $tiny
  margin-bottom: $small

.inv-row
  display: flex
  align-items: center
  gap: $small

// Under the invitation it is about, one list step from it.
.inv-refusal
  --action-result-margin: #{$tiny} 0 0

.inv-type,
.inv-sent
  color: $text-muted
  font-size: $secondary-font-size

// A field and its button in one line: the field takes what the button
// leaves, down to 80.
.invite-row
  display: flex
  align-items: flex-start
  gap: $small

.invite-row__who
  flex: 1 1 0
  min-width: $grid-step * 20
</style>
