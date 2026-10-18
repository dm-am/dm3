<script setup lang="ts">
/**
 * RolesSection — manage blog staff. The two operations answer to different
 * intentions: BlogIntention.InviteAssistant is the owner's alone, while
 * removal (BlogService.RemoveAssistant) asks BlogIntention.Edit, which the
 * owner and administration hold. The host page computes both and passes them
 * in; everyone else the settings page admits reads the roster without
 * controls. Mirrors the game RolesSection; blogs expose no per-blog mentor
 * field on the DTO, so there is no mentor row here.
 *
 * Inviting an assistant says nothing in the corner and cannot say it with the
 * roster either: the invited person joins the list of assistants only once the
 * invitation is accepted. What it does change is the block below the roster,
 * which names everyone whose invitation is out and when it was sent - the same
 * answer the game section gives, and read from the same place (see
 * `loadPending`).
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
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";

defineProps<{
  /** Whether the viewer may invite an assistant. */
  canInvite: boolean;
  /** Whether the viewer may remove an assistant. */
  canRemove: boolean;
}>();

const store = useBlogDetailsStore();
const { blog } = storeToRefs(store);

const assistants = computed(() => blog.value?.assistants ?? []);

const inviteUsername = ref("");
const inviting = ref(false);
const pendingRemove = ref<{ username: string } | null>(null);

const pending = ref<BlogInvitation[]>([]);

const sentAt = (invitation: BlogInvitation) =>
  new Date(invitation.createdUtc).getTime();

/** When the invitation went out, in the row that stands for it. */
const sentLabel = (invitation: BlogInvitation) =>
  `отправлено ${formatDateFull(invitation.createdUtc)}`;

/**
 * The assistant invitations that are still out, oldest first, so the one just
 * sent is the row next to the control that sent it.
 *
 * Read from the invitation endpoint, because the blog itself does not carry
 * them: the blog DTO stops at the assistants who have already accepted. The
 * read asks BlogIntention.EditSettings, which is also what admits a viewer to
 * the settings page, so everyone who is shown this section may make it.
 *
 * The kind is spelled the way the rest of the blog API and the game twin spell
 * roles, in lower case (BlogRoleExtensions.ToApiString).
 */
async function loadPending() {
  if (!blog.value) return;
  const { data, error } = await blogApi.getInvitations(blog.value.id);
  // A read that failed leaves the block as it is rather than emptying it over
  // an invitation that is out.
  if (error) return;
  pending.value = (data?.resources ?? [])
    .filter((invitation) => invitation.type === "assistant")
    .sort((a, b) => sentAt(a) - sentAt(b));
}

onMounted(loadPending);

/** A refused invitation, under the row it was typed in, the name kept. */
const inviteResult = useActionResult();

async function invite() {
  if (!blog.value || !inviteUsername.value.trim()) return;
  inviting.value = true;
  inviteResult.clear();
  const { data, error } = await blogApi.inviteAssistant(
    blog.value.id,
    inviteUsername.value.trim(),
  );
  inviting.value = false;
  if (error) {
    inviteResult.refuse(error, "Не удалось пригласить ассистента");
    return;
  }
  // The row from the server's own answer, in place of the invitation it has
  // just replaced: a second invitation to the same person invalidates the
  // first (BlogInvitationService.CreateInvitation), so what changes in the
  // block is the moment it was sent. Put in before the reload, which is then
  // free to fail without taking the row with it.
  if (data) {
    pending.value = [
      ...pending.value.filter(
        (i) => i.invitedUser.username !== data.invitedUser.username,
      ),
      data,
    ];
  }
  inviteUsername.value = "";
  // Not the blog: an invitation adds nobody to the roster, and the roster is
  // all the blog carries.
  await loadPending();
}

// The request of the confirmation. The window waits for it, prints a refusal in
// itself and closes on success alone, so the interceptor keeps quiet about it.
async function confirmRemove() {
  const target = pendingRemove.value;
  if (!blog.value || !target) return;
  const result = await blogApi.removeAssistant(blog.value.id, target.username, {
    ownsRefusal: true,
    ownsRateLimit: true,
  });
  if (!result.error) void store.loadBlog(blog.value.id);
  return result;
}
</script>

<template>
  <SettingsSection title="Управление ролями">
    <div class="role-block">
      <div class="role-label">Ассистенты</div>
      <ul v-if="assistants.length" class="staff-list">
        <li v-for="a in assistants" :key="a.id" class="staff-item">
          <UserLink :user="a" />
          <RemoveButton
            v-if="canRemove"
            @click="pendingRemove = { username: a.username }"
          >
            Удалить
          </RemoveButton>
        </li>
      </ul>
      <SecondaryText v-else>Ассистентов нет</SecondaryText>

      <div v-if="pending.length" class="pending">
        <div class="role-label">Ожидают подтверждения</div>
        <ul class="staff-list">
          <li v-for="inv in pending" :key="inv.id" class="staff-item">
            <UserLink :user="inv.invitedUser" />
            <span class="sent">{{ sentLabel(inv) }}</span>
          </li>
        </ul>
      </div>

      <div v-if="canInvite" class="invite-row">
        <UserAutocomplete
          v-model="inviteUsername"
          placeholder="Имя пользователя"
        />
        <Button
          type="button"
          variant="primary"
          :loading="inviting"
          :disabled="!inviteUsername.trim()"
          @click="invite"
        >
          Пригласить
        </Button>
      </div>
      <ActionResult v-if="canInvite" :result="inviteResult" />
    </div>

    <ConfirmDialog
      :show="!!pendingRemove"
      title="Удалить ассистента?"
      message="Ассистент потеряет права в блоге"
      confirm-label="Удалить"
      failure="Не удалось удалить ассистента"
      :action="confirmRemove"
      @update:show="(shown) => !shown && (pendingRemove = null)"
    />
  </SettingsSection>
</template>

<style scoped lang="sass">
.role-block
  & + &
    margin-top: $medium
    padding-top: $medium
    border-top: 1px solid $border

.role-label
  color: $text-muted
  font-size: $secondary-font-size
  margin-bottom: $small

.staff-list
  list-style: none
  display: flex
  flex-direction: column
  gap: $tiny

.staff-item
  display: flex
  align-items: center
  gap: $small

.pending
  margin-top: $small

.sent
  color: $text-muted
  font-size: $secondary-font-size

.invite-row
  display: flex
  align-items: flex-start
  gap: $small
  margin-top: $small
</style>
