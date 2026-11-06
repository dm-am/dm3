<script setup lang="ts">
/** A post of a game room assembled from its parts; what stays here is what they share. */
import { ref, computed } from "vue";
import dayjs from "dayjs";
import { storeToRefs } from "pinia";
import type { Post, Room } from "@/entities/game";
import { postApi, DiceRolls, GameLink, RoomLink } from "@/entities/game";
import { Tooltip } from "@/shared/ui/Tooltip";
import { TruncatedContent } from "@/shared/ui/TruncatedContent";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { userIsModerator } from "@/entities/user";
import {
  AttachmentsClip,
  AttachmentsEditor,
  AttachmentsStrip,
  audienceCandidates,
  reserveAttachmentImageBoxes,
  useAttachmentsFold,
} from "@/features/upload";
import { trimHtmlWhitespace } from "@/shared/lib/utils/trimHtmlWhitespace";
import { useAuthStore } from "@/shared/stores/auth";
import { symbols } from "@/shared/lib/utils/icons";
import { EDIT_TIME_LIMIT_MINUTES } from "@/shared/lib/utils/chat";
import GamePostActions from "./GamePostActions.vue";
import GamePostAuthor from "./GamePostAuthor.vue";
import GamePostEdit from "./GamePostEdit.vue";
import GamePostText from "./GamePostText.vue";
import PostReviewSum from "./PostReviewSum.vue";
import PostReviews from "./PostReviews.vue";
import { usePostReviews } from "./usePostReviews";
import { usePostAttachments } from "./usePostAttachments";
import { useEditAttachments } from "./useEditAttachments";
import { usePostTextBudget } from "./usePostTextBudget";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";
import { unwrapResource } from "@/shared/api";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";

const props = withDefaults(
  defineProps<{
    post: Post;
    number?: number;
    /** Featured post mode — shows navigation breadcrumb, anchor icon instead of number */
    showNavigation?: boolean;
    /** "game" names the game and the room; "room" the room alone, on a page that is the game. */
    navigationLevel?: "game" | "room";
    /** Enable content truncation */
    truncatable?: boolean;
    /** Search query for highlighting matches in post text */
    searchQuery?: string;
    /** Edit and delete on the post; only the game room opts in. */
    editable?: boolean;
    /** The room with its accesses: whom the author can show a file to in the edit. */
    room?: Room | null;
  }>(),
  {
    showNavigation: false,
    navigationLevel: "game",
    truncatable: false,
    editable: false,
  },
);

const emit = defineEmits<{
  /** The post was soft-deleted (parent may refetch / drop it). */
  deleted: [id: string];
}>();

const { user: currentUser } = storeToRefs(useAuthStore());

// The server's rendering of a saved edit, kept so the post updates in place.
const gameTextOverride = ref<string | null>(null);
const metaTextOverride = ref<string | null>(null);
// When the edit this page made was saved, for the edit line under the text.
const editedAtOverride = ref<string | null>(null);

const effectiveGameText = computed(
  () => gameTextOverride.value ?? props.post.gameText,
);
const effectiveMetaText = computed(
  () => metaTextOverride.value ?? props.post.metagameText,
);

const hasMetagameText = computed(() => !!effectiveMetaText.value);

const postId = computed(() => props.post.id);
const postAnchor = computed(() => `#post-${postId.value}`);

const attachments = usePostAttachments(
  () => postId.value,
  () => props.post.attachments,
);
const editAttachments = useEditAttachments(() => postId.value, attachments);

// Trimmed, and the box of every own attachment picture declared, so a decode moves nothing.
const postTextHtml = computed(() =>
  reserveAttachmentImageBoxes(
    trimHtmlWhitespace(effectiveGameText.value),
    attachments.list,
  ),
);
const postMetagameHtml = computed(() =>
  reserveAttachmentImageBoxes(
    trimHtmlWhitespace(effectiveMetaText.value),
    attachments.list,
  ),
);

// post.room.game is a full GameRef from the server: GameLink needs no fetch of its own.
const navigation = computed(() => {
  const room = props.post.room;
  return props.showNavigation && room?.game?.publicId
    ? { room, game: room.game }
    : null;
});

const gameId = computed(() => props.post.room?.game?.publicId);
const roomNumber = computed(() => props.post.room?.roomNumber);

// The room page anchored at the post; elsewhere window.location is another page.
const postRoute = computed(() =>
  gameId.value && roomNumber.value
    ? {
        name: "game-room" as const,
        params: { id: gameId.value, num: roomNumber.value },
        hash: postAnchor.value,
      }
    : null,
);

// The author's column, whose height the text beside it is measured by.
const metaRef = ref<HTMLElement | null>(null);
const dynamicMaxHeight = usePostTextBudget(metaRef);

const isDeleted = ref(false);
const showDeleteConfirm = ref(false);

const isModerator = computed(() => userIsModerator(currentUser.value));
const isPostAuthor = computed(
  () =>
    !!currentUser.value &&
    currentUser.value.username === props.post.author?.username,
);
const withinEditWindow = computed(
  () =>
    dayjs().diff(dayjs(props.post.createdUtc), "minute", true) <=
    EDIT_TIME_LIMIT_MINUTES,
);
// Edit: the author within the window, or a moderator the server lets edit, who reads every
// hidden block of the post. Delete: the author alone, nothing in a game room is moderated.
const canEditPost = computed(
  () =>
    props.editable &&
    !isDeleted.value &&
    (isPostAuthor.value
      ? withinEditWindow.value
      : isModerator.value && props.post.viewer?.canEdit === true),
);
const canDeletePost = computed(
  () => props.editable && !isDeleted.value && isPostAuthor.value,
);
// The latest edit leads the history the server sends.
const editedAt = computed(
  () => editedAtOverride.value ?? props.post.edits?.[0]?.modifiedUtc ?? null,
);
const isEdited = computed(
  () => !!editedAt.value || (props.post.edits?.length ?? 0) > 0,
);

const isEditingPost = ref(false);

// The line under the text; a post with nothing to say there has none.
const showEditLine = computed(
  () =>
    !isEditingPost.value &&
    (isEdited.value || canEditPost.value || canDeletePost.value),
);

/** The author's text as typed, the editor's seed: the display one has [private] flattened. */
const editSource = ref({ gameText: "", metagameText: "" });
const openingEdit = ref(false);

/** A refused or empty read of the source, under the edit line: the editor opens on the source alone. */
const openResult = useActionResult();
const openEmpty = ref("");
const SOURCE_FAILED = "Не удалось загрузить текст поста";

async function startEditPost() {
  if (openingEdit.value || isEditingPost.value) return;
  openResult.clear();
  openEmpty.value = "";
  attachNotice.value = "";
  editAttachments.forget();
  openingEdit.value = true;
  const { data, error } = await postApi.getPostForEdit(postId.value);
  openingEdit.value = false;
  if (error) {
    openResult.refuse(error, SOURCE_FAILED);
    return;
  }
  const source = unwrapResource<Post>(data);
  if (!source?.gameText) {
    openEmpty.value = SOURCE_FAILED;
    return;
  }
  editSource.value = {
    gameText: source.gameText,
    metagameText: source.metagameText ?? "",
  };
  isEditingPost.value = true;
}

/** A file the saved edit failed to attach, said under the edit line, as the form of a post says it. */
const attachNotice = ref("");

/** The edit saved: its files go, then the texts the server rendered take the old ones' place. */
async function onEditSaved(edit: {
  gameText: string;
  metagameText: string;
  editedAt: string;
}) {
  const failedNames = await editAttachments.send();
  if (failedNames.length > 0) {
    attachNotice.value =
      "Пост сохранен, но файлы не приложились: " +
      `${failedNames.join(", ")}. Приложите их через правку поста.`;
  }
  gameTextOverride.value = edit.gameText;
  metaTextOverride.value = edit.metagameText;
  editedAtOverride.value = edit.editedAt;
  isEditingPost.value = false;
}

function cancelEditPost() {
  editAttachments.forget();
  isEditingPost.value = false;
}

const audienceChoices = computed(() =>
  audienceCandidates(props.room?.accesses, props.post.author?.username),
);

function requestDeletePost() {
  showDeleteConfirm.value = true;
}

/** The confirmation's action: the post goes only once the server took it down. */
async function deletePost() {
  const id = postId.value;
  if (!id) return;
  const result = await postApi.deletePost(id, OWNS_REFUSAL);
  if (!result.error) {
    isDeleted.value = true;
    emit("deleted", id);
  }
  return result;
}

const reviews = usePostReviews({
  post: () => props.post,
  isDeleted: () => isDeleted.value,
  isPostAuthor: () => isPostAuthor.value,
  postRoute: () => postRoute.value,
  postAnchor: () => postAnchor.value,
});

// The editor lists the files itself; the clip would open a second list beside it.
const hasAttachmentsClip = computed(
  () => attachments.list.length > 0 && !isEditingPost.value,
);
const attachmentsStripId = computed(() => `post-attachments-${postId.value}`);
const { open: attachmentsOpen, toggle: toggleAttachments } = useAttachmentsFold(
  () => attachments.list.length,
);
</script>

<template>
  <article
    :id="`post-${postId}`"
    class="game-post"
    :class="{ featured: !!navigation }"
  >
    <!-- The breadcrumb of a featured post -->
    <div v-if="navigation" class="post-nav">
      <template v-if="navigationLevel === 'game'">
        <GameLink :game="navigation.game" />
        <span class="nav-separator" aria-hidden="true"> > </span>
      </template>
      <RoomLink :room="navigation.room" :game="navigation.game" />
    </div>

    <div class="post-card" :class="{ 'has-attachments': hasAttachmentsClip }">
      <div v-if="isDeleted" class="post-deleted">Пост удален</div>

      <template v-else>
        <!-- The files: a clip in the corner the number mirrors -->
        <AttachmentsClip
          v-if="hasAttachmentsClip"
          :count="attachments.list.length"
          :open="attachmentsOpen"
          :controls="attachmentsStripId"
          @toggle="toggleAttachments"
        />

        <div class="post-columns">
          <div ref="metaRef" class="post-meta">
            <GamePostAuthor :post="post">
              <PostReviewSum :reviews="reviews" />
            </GamePostAuthor>
          </div>

          <div class="post-body">
            <GamePostEdit
              v-if="isEditingPost"
              :post-id="post.id"
              :game-text="editSource.gameText"
              :metagame-text="editSource.metagameText"
              :is-moderator="isModerator"
              @saved="onEditSaved"
              @cancel="cancelEditPost"
            >
              <template #attachments>
                <!-- Only the author attaches and marks: the server refuses everyone else -->
                <AttachmentsEditor
                  :attachments="attachments.list"
                  :pending="editAttachments.pending"
                  :busy="attachments.busy"
                  :is-author="isPostAuthor"
                  :full="editAttachments.full"
                  :error="attachments.error"
                  :candidates="audienceChoices"
                  @choose="editAttachments.choose"
                  @unchoose="editAttachments.unchoose"
                  @mark-pending="editAttachments.mark"
                  @mark="attachments.mark"
                  @remove="attachments.remove"
                />
              </template>
            </GamePostEdit>

            <template v-else>
              <AttachmentsStrip
                v-if="attachments.list.length"
                :id="attachmentsStripId"
                :attachments="attachments.list"
                :open="attachmentsOpen"
              />

              <TruncatedContent
                :truncatable="truncatable"
                :max-height="dynamicMaxHeight"
                :watch-key="postTextHtml"
              >
                <GamePostText
                  kind="game"
                  :html="postTextHtml"
                  :search-query="searchQuery"
                />

                <DiceRolls
                  v-if="post.diceRolls?.length"
                  :rolls="post.diceRolls"
                />

                <GamePostText
                  v-if="hasMetagameText"
                  kind="metagame"
                  :html="postMetagameHtml"
                  :search-query="searchQuery"
                />
              </TruncatedContent>

              <GamePostActions
                v-if="showEditLine"
                :edited-at="editedAt"
                :is-edited="isEdited"
                :can-edit="canEditPost"
                :can-delete="canDeletePost"
                :opening="openingEdit"
                @edit="startEditPost"
                @delete="requestDeletePost"
              />
              <ActionResult
                :result="openResult"
                :text="openEmpty || attachNotice"
                class="post-edit-refusal"
              />
            </template>
          </div>
        </div>

        <!-- The number or the anchor alone, in the corner, as on DM2 -->
        <div class="post-footer">
          <Tooltip v-if="navigation" text="Перейти к посту">
            <router-link class="post-link" :to="postRoute!">{{
              symbols.returnArrow
            }}</router-link>
          </Tooltip>
          <!-- The span is the flex child: Tooltip renders a trigger and a teleport -->
          <span v-else-if="number" class="post-number-tip">
            <Tooltip text="Перейти к посту">
              <a class="post-number" :href="postAnchor">{{ number }}</a>
            </Tooltip>
          </span>
        </div>
      </template>
    </div>

    <!-- Outside the deleted branch, so the answer still has a dialog to close -->
    <ConfirmDialog
      v-model:show="showDeleteConfirm"
      title="Удалить пост?"
      message="Пост будет удален, вернуть его сможет только модерация"
      confirm-label="Удалить"
      failure="Не удалось удалить пост"
      :action="deletePost"
    />

    <PostReviews :reviews="reviews" :editable="editable" />
  </article>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *
@use "@/assets/styles/PostCard" as *

// Navigation (featured post only, above card)
.post-nav
  margin-bottom: $small

.nav-separator
  color: $text-muted

// DM2's .commentItem; positioned for the clip in its corner.
.post-card
  position: relative
  padding: 5px
  background-color: $bg-element
  border: 1px dashed $border

// Under the edit line, at the left edge of the text it stands under.
.post-edit-refusal
  --action-result-margin: #{$small} 5px 0 #{$text-inset}

// DM2's table of three cells, at every width.
.post-columns
  display: flex
  gap: $text-inset
  align-items: flex-start

// DM2's td.firstrow of 140 on a step of 20.
.post-meta
  flex-shrink: 0
  width: 140px
  text-align: left
  font-size: $font-size
  line-height: 1.25

// DM2's td.thirdrow: 11px above and below, the margin of its div.dmtxt.
.post-body
  flex: 1
  min-width: 0
  padding: $block-margin 0
  overflow-wrap: anywhere
  color: $text

// "... показать полностью" at the left edge of the text, as under a comment.
.post-body :deep(.truncated-expand-button)
  margin-left: $text-inset

// The rolls between the texts: from the text's left edge to the column's right, 22px of air.
.dice-rolls
  margin: ($block-margin * 2) 0 ($block-margin * 2) $text-inset

  // The last block of the post: the column adds its own 11px under it.
  &:last-child
    margin-bottom: $block-margin

// The number or the arrow as far from the right border as the character name is
// from the left: the card's 5px and the 5px margin of the author column.
.post-footer
  display: flex
  justify-content: flex-end
  padding-right: 5px
  line-height: 15px

// The wrapper takes the number's size so its line is the number's line, and 2px under it
// set the number's ink 10px above the bottom border, as far as from the right one.
.post-number-tip
  padding-bottom: $tiny
  font-size: $tertiary-font-size

// Link colour: every "go to the post" looks like a link.
.post-number
  font-size: $tertiary-font-size
  +link-tone($link)

.post-link
  font-size: $secondary-font-size
  +link-tone($link)

.post-deleted
  padding: $small
  color: $text-muted
  font-style: italic

// The clip mirrors the number: its ink as far from the top and right borders.
.attachments-toggle
  position: absolute
  top: 10.6px
  right: 10.55px

  &.counted
    right: 10px

// The strip over the text, its left edge on the text's, 22px of air under it.
.attachments-fold :deep(.attachments-strip)
  margin: $medium 0 $block-margin $text-inset
</style>
