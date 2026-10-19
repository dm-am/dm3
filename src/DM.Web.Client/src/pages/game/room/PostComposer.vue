<script setup lang="ts">
// The form of a post; "Отправить" sends the post, then its files.
import { computed, ref, watch } from "vue";
import { storeToRefs } from "pinia";
import {
  DiceComposer,
  postApi,
  RoomAccessPolicy,
  useGameDetailsStore,
  type DiceRollInput,
  type Room,
} from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import {
  AttachmentsPicker,
  audienceCandidates,
  uploadPostAttachments,
  type PendingAttachment,
} from "@/features/upload";
import { Select, type SelectOption } from "@/shared/ui/Select";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import { BODY_TEXT_MAX_LENGTH } from "@/shared/lib/constants/content";
import { refusalHere } from "@/shared/lib/errors";
import { ActionResult } from "@/shared/ui/ActionResult";
import Button from "@/shared/ui/Button/Button.vue";
import PostPendencyField from "./PostPendencyField.vue";

const props = defineProps<{
  room: Room | null | undefined;
  gameId: string;
}>();

const emit = defineEmits<{
  posted: [];
}>();

const { characters, isMaster, isAssistant } = storeToRefs(
  useGameDetailsStore(),
);
const { user } = storeToRefs(useAuthStore());

const MASTER_VALUE = "__master__";
const canManageTurns = computed(() => isMaster.value || isAssistant.value);

// An open room is open to read; writing in it still takes the access
// (RoomIntentionResolver.CreatePost).
const writingCharacterIds = computed(() => {
  const admitted = new Set<string>();
  for (const access of props.room?.accesses ?? []) {
    const id = access.character?.id;
    if (id && access.policy === RoomAccessPolicy.Full) admitted.add(id);
  }
  return admitted;
});
const myActiveCharacters = computed(() =>
  characters.value.filter(
    (c) =>
      c.status === "Active" &&
      c.author?.username === user.value?.username &&
      writingCharacterIds.value.has(c.id),
  ),
);
// An NPC writes under the same access a player's character does.
const activeNpcs = computed(() =>
  characters.value.filter(
    (c) =>
      c.status === "Active" && c.isNpc && writingCharacterIds.value.has(c.id),
  ),
);

const showComposer = computed(
  () =>
    !!user.value &&
    (myActiveCharacters.value.length > 0 || canManageTurns.value),
);

const postAsOptions = computed<SelectOption[]>(() => {
  const options: SelectOption[] = myActiveCharacters.value.map((c) => ({
    value: c.id,
    label: c.name,
  }));
  if (canManageTurns.value) {
    for (const npc of activeNpcs.value) {
      options.push({
        value: npc.id,
        label: `${npc.name} (неигровой персонаж)`,
      });
    }
    options.push({ value: MASTER_VALUE, label: "От лица мастера" });
  }
  return options;
});

const selectedPostAs = ref("");
watch(
  postAsOptions,
  (options) => {
    if (!options.some((o) => o.value === selectedPostAs.value)) {
      selectedPostAs.value = options[0]?.value ?? "";
    }
  },
  { immediate: true },
);

const gameText = ref("");
const metagameText = ref("");
const submitting = ref(false);
const composerError = ref<string | null>(null);

// Uploaded after the post exists: an attachment names its post.
const pendingFiles = ref<PendingAttachment[]>([]);
const audienceChoices = computed(() =>
  audienceCandidates(props.room?.accesses, user.value?.username),
);
const attachmentError = ref<string | null>(null);
const gameEditorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);
const metaEditorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);

const diceEnabled = computed(() => props.room?.settings?.diceEnabled ?? false);
const diceRolls = ref<DiceRollInput[]>([]);

async function submitPost() {
  if (!gameText.value.trim() || submitting.value || !props.room?.id) return;
  submitting.value = true;
  composerError.value = null;
  attachmentError.value = null;

  const { data, error } = await postApi.createPost(props.room.id, {
    characterId:
      selectedPostAs.value && selectedPostAs.value !== MASTER_VALUE
        ? selectedPostAs.value
        : undefined,
    gameText: gameText.value,
    metagameText: metagameText.value.trim() ? metagameText.value : undefined,
    diceRolls:
      diceEnabled.value && diceRolls.value.length ? diceRolls.value : undefined,
  });

  if (error) {
    submitting.value = false;
    composerError.value = refusalHere(error, "Не удалось отправить пост");
    return;
  }

  await attachPending(data?.resource?.id);

  submitting.value = false;

  gameText.value = "";
  metagameText.value = "";
  diceRolls.value = [];
  // Failed files belong to the published post, not to the next one.
  pendingFiles.value = [];
  gameEditorRef.value?.clear();
  metaEditorRef.value?.clear();
  emit("posted");
}

// A published post with a failed file is said plainly; the edit retries it.
async function attachPending(postId: string | undefined) {
  if (!postId || pendingFiles.value.length === 0) return;
  const { failedNames } = await uploadPostAttachments(
    postId,
    pendingFiles.value,
  );
  pendingFiles.value = pendingFiles.value.filter((f) =>
    failedNames.includes(f.file.name),
  );
  if (failedNames.length > 0) {
    composerError.value =
      "Пост отправлен, но файлы не приложились: " +
      `${failedNames.join(", ")}. Приложите их через правку поста.`;
  }
}
</script>

<template>
  <!-- DM2's form of a post: its captions, its order, no frame or heading. -->
  <section v-if="showComposer" class="composer">
    <div v-if="postAsOptions.length > 1" class="composer-as">
      <label class="composer-label" for="composer-post-as">От лица:</label>
      <Select
        id="composer-post-as"
        v-model="selectedPostAs"
        :options="postAsOptions"
        class="composer-as-select"
      />
    </div>

    <span class="composer-label">Игровой текст:</span>
    <BBCodeEditor
      ref="gameEditorRef"
      v-model="gameText"
      placeholder=""
      context="post"
      :draft-key="composerDraftKey('room', 'post', room?.id)"
      :disabled="submitting"
      :min-height="120"
      :max-length="BODY_TEXT_MAX_LENGTH"
      :is-moderator="canManageTurns"
    />

    <div v-if="diceEnabled" class="dice">
      <div class="dice-title">
        <span id="composer-dice-caption">Броски кубиков</span>
        (<button
          type="button"
          class="dice-clear"
          :disabled="submitting"
          @click="diceRolls = []"
        >
          Очистить</button
        >):
      </div>
      <DiceComposer
        v-model="diceRolls"
        aria-labelledby="composer-dice-caption"
        :disabled="submitting"
      />
    </div>

    <span class="composer-label">Метаигровой текст:</span>
    <!-- The check of a comment: the metagame text refuses a hidden block. -->
    <BBCodeEditor
      ref="metaEditorRef"
      v-model="metagameText"
      placeholder=""
      context="post"
      check-context="common"
      :draft-key="composerDraftKey('room', 'metagame', room?.id)"
      :disabled="submitting"
      :min-height="60"
      :max-height="200"
      :max-length="BODY_TEXT_MAX_LENGTH"
      :is-moderator="canManageTurns"
    />

    <AttachmentsPicker
      v-model="pendingFiles"
      v-model:error="attachmentError"
      :disabled="submitting"
      :candidates="audienceChoices"
    />

    <!-- Acts at once, not with the post. -->
    <PostPendencyField v-if="canManageTurns" :room="room" :game-id="gameId" />

    <ActionResult :text="composerError" />

    <Button
      type="button"
      variant="primary"
      class="composer-submit"
      :disabled="submitting || !gameText.trim()"
      @click="submitPost"
    >
      Отправить
    </Button>
  </section>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/Links" as *

.composer
  margin-top: $big

.composer-as
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small
  margin-bottom: $small

// As wide as its longest name, never wider than the column.
.composer-as .composer-as-select
  width: auto
  max-width: 100%
  min-width: 0

.composer-label
  display: block
  margin: $medium 0 $minor
  +field-caption

.composer > .composer-label:first-child
  margin-top: 0

.composer-as .composer-label
  margin: 0

.composer-submit
  margin-top: $medium

.dice
  margin-top: $medium

.dice-title
  margin-bottom: $small
  +field-caption

.dice-clear
  +inline-link-button
</style>
