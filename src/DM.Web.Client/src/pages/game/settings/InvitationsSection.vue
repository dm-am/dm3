<script setup lang="ts">
/**
 * InvitationsSection — outstanding invitations for the game plus the controls
 * to invite a player or a reader and to cancel a pending invitation. Backed by
 * the invitation gameApi; the list is held locally (not part of the shared
 * game store).
 */
import { computed, onMounted, ref } from "vue";
import { storeToRefs } from "pinia";
import { useGameDetailsStore, gameApi, type Invitation } from "@/entities/game";
import { UserLink } from "@/entities/user";
import { UserAutocomplete } from "@/entities/user";
import { Select } from "@/shared/ui/Select";
import Button from "@/shared/ui/Button/Button.vue";
import { SettingsSection } from "@/shared/ui/SettingsSection";
import { RemoveButton } from "@/shared/ui/RemoveButton";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { useToast } from "@/shared/lib/composables/useToast";
import { notifyFailure } from "@/shared/lib/errors";

const store = useGameDetailsStore();
const { game } = storeToRefs(store);
const toast = useToast();

const invitations = ref<Invitation[]>([]);
const loading = ref(false);

const username = ref("");
const kind = ref<"player" | "reader">("player");
const inviting = ref(false);

const kindOptions = [
  { value: "player", label: "Игрок" },
  { value: "reader", label: "Читатель" },
];

const typeLabels: Record<string, string> = {
  player: "Игрок",
  reader: "Читатель",
  assistant: "Ассистент",
};

const gameGuid = computed(() => game.value?.id ?? "");

async function load() {
  if (!game.value) return;
  loading.value = true;
  const { data } = await gameApi.getGameInvitations(
    game.value.publicId ?? game.value.id,
  );
  invitations.value = data?.resources ?? [];
  loading.value = false;
}

onMounted(load);

async function invite() {
  if (!gameGuid.value || !username.value.trim()) return;
  inviting.value = true;
  const { error } =
    kind.value === "player"
      ? await gameApi.invitePlayer(gameGuid.value, username.value.trim())
      : await gameApi.inviteReader(gameGuid.value, username.value.trim());
  inviting.value = false;
  if (error) {
    notifyFailure(error, "Не удалось отправить приглашение");
    return;
  }
  toast.success("Приглашение отправлено");
  username.value = "";
  await load();
}

async function cancel(invitation: Invitation) {
  if (!gameGuid.value) return;
  const { error } = await gameApi.cancelInvitation(
    gameGuid.value,
    invitation.id,
  );
  if (error) {
    notifyFailure(error, "Не удалось отменить приглашение");
    return;
  }
  toast.success("Приглашение отменено");
  await load();
}
</script>

<template>
  <SettingsSection title="Приглашения">
    <ul v-if="invitations.length" class="inv-list">
      <li v-for="inv in invitations" :key="inv.id" class="inv-item">
        <UserLink :user="inv.invitedUser" />
        <span class="inv-type">{{ typeLabels[inv.type] ?? inv.type }}</span>
        <RemoveButton @click="cancel(inv)">Отменить</RemoveButton>
      </li>
    </ul>
    <SecondaryText v-else-if="!loading">Активных приглашений нет</SecondaryText>

    <div class="invite-row">
      <UserAutocomplete v-model="username" placeholder="Имя пользователя" />
      <Select
        :model-value="kind"
        :options="kindOptions"
        aria-label="Тип приглашения"
        @update:model-value="(v) => (kind = v as 'player' | 'reader')"
      />
      <Button
        type="button"
        :loading="inviting"
        :disabled="!username.trim()"
        @click="invite"
      >
        Пригласить
      </Button>
    </div>
  </SettingsSection>
</template>

<style scoped lang="sass">
.inv-list
  list-style: none
  display: flex
  flex-direction: column
  gap: $tiny
  margin-bottom: $small

.inv-item
  display: flex
  align-items: center
  gap: $small

.inv-type
  color: $text-muted
  font-size: $secondary-font-size

.invite-row
  display: flex
  align-items: flex-start
  gap: $small
  flex-wrap: wrap
</style>
