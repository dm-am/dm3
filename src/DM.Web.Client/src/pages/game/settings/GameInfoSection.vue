<script setup lang="ts">
/**
 * GameInfoSection — edit the game's core details (title / system / setting),
 * recruitment (open + PC limit) and privacy settings. Persists via
 * gameApi.updateGame (PATCH games/{id}/details) then reloads the game.
 *
 * The public description (info) is intentionally NOT edited here: the details
 * endpoint returns it as server-rendered HTML (InfoBbText), so there is no raw
 * BBCode source to round-trip through an editor without corrupting it. Editing
 * the description needs a raw-source endpoint that does not exist yet.
 */
import { ref, watch } from "vue";
import { storeToRefs } from "pinia";
import {
  useGameDetailsStore,
  gameApi,
  CommentariesAccessMode,
  type Game,
} from "@/entities/game";
import { Form, FormField } from "@/shared/ui/Form";
import { Select } from "@/shared/ui/Select";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import { useToast } from "@/shared/lib/composables/useToast";

const store = useGameDetailsStore();
const { game } = storeToRefs(store);
const toast = useToast();

const commentariesOptions = [
  { value: CommentariesAccessMode.Public, label: "Публичные" },
  { value: CommentariesAccessMode.Readonly, label: "Только для чтения" },
  { value: CommentariesAccessMode.Private, label: "Только для участников" },
];

const title = ref("");
const system = ref("");
const setting = ref("");
const isRecruitmentOpen = ref(false);
const pcLimit = ref("");
const viewPrivates = ref(false);
const viewDice = ref(false);
const commentariesAccess = ref(CommentariesAccessMode.Public);

const saving = ref(false);

// Seed the local draft from the loaded game.
watch(
  game,
  (g) => {
    if (!g) return;
    title.value = g.title ?? "";
    system.value = g.system ?? "";
    setting.value = g.setting ?? "";
    isRecruitmentOpen.value = g.recruitment?.isOpen ?? false;
    pcLimit.value =
      g.recruitment?.pcLimit != null ? String(g.recruitment.pcLimit) : "";
    viewPrivates.value = g.privacySettings?.viewPrivates ?? false;
    viewDice.value = g.privacySettings?.viewDice ?? false;
    commentariesAccess.value =
      g.privacySettings?.commentariesAccess ?? CommentariesAccessMode.Public;
  },
  { immediate: true },
);

async function save() {
  if (!game.value) return;
  saving.value = true;

  const parsedLimit = pcLimit.value.trim()
    ? parseInt(pcLimit.value, 10)
    : undefined;
  const patch: Partial<Game> = {
    title: title.value.trim(),
    system: system.value.trim(),
    setting: setting.value.trim(),
    recruitment: {
      ...game.value.recruitment,
      isOpen: isRecruitmentOpen.value,
      pcLimit: Number.isFinite(parsedLimit) ? parsedLimit : undefined,
    },
    privacySettings: {
      viewPrivates: viewPrivates.value,
      viewDice: viewDice.value,
      commentariesAccess: commentariesAccess.value,
    },
  };

  const { error } = await gameApi.updateGame(game.value.id, patch);
  saving.value = false;
  if (error) {
    toast.error("Не удалось сохранить информацию игры");
    return;
  }
  toast.success("Информация игры сохранена");
  await store.loadGame(game.value.publicId ?? game.value.id);
}
</script>

<template>
  <section class="settings-section">
    <BlockTitle>Информация игры</BlockTitle>
    <Form
      :valid="title.trim().length > 0"
      :loading="saving"
      action="Сохранить"
      @submit="save"
    >
      <FormField label="Название" name="game-title">
        <input id="game-title" v-model="title" type="text" maxlength="200" />
      </FormField>
      <FormField label="Система" name="game-system">
        <input id="game-system" v-model="system" type="text" maxlength="100" />
      </FormField>
      <FormField label="Сеттинг" name="game-setting">
        <input
          id="game-setting"
          v-model="setting"
          type="text"
          maxlength="100"
        />
      </FormField>

      <FormField>
        <label class="checkbox-label">
          <input v-model="isRecruitmentOpen" type="checkbox" />
          Набор открыт
        </label>
      </FormField>
      <FormField label="Лимит игроков" name="pc-limit">
        <input
          id="pc-limit"
          v-model="pcLimit"
          type="text"
          inputmode="numeric"
          placeholder="без ограничения"
        />
      </FormField>

      <FormField>
        <label class="checkbox-label">
          <input v-model="viewPrivates" type="checkbox" />
          Показывать приватные сообщения
        </label>
      </FormField>
      <FormField>
        <label class="checkbox-label">
          <input v-model="viewDice" type="checkbox" />
          Показывать результаты бросков
        </label>
      </FormField>
      <FormField label="Доступ к комментариям">
        <Select
          :model-value="commentariesAccess"
          :options="commentariesOptions"
          @update:model-value="
            (v) => (commentariesAccess = v as CommentariesAccessMode)
          "
        />
      </FormField>
    </Form>
  </section>
</template>

<style scoped lang="sass">
.settings-section
  margin-bottom: $big
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius

.checkbox-label
  display: flex
  align-items: center
  gap: $small
  cursor: pointer
</style>
