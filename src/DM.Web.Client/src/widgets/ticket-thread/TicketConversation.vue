<script setup lang="ts">
/**
 * The conversation of a ticket, under its block (TicketThread): the answers of
 * its author and of moderation, and the form to answer in.
 *
 * A ticket works as a topic with comments and looks like one, so an answer is
 * the site's comment (CommentItem) in its compact look, the look every comment
 * of DM2 has, whatever the reader chose for the discussions. There is no
 * filter and no paging: a conversation is a hundred answers at most and comes
 * whole with the ticket.
 *
 * An answer stands to be read and nothing more. A ticket is the record of how
 * it was settled, so nothing in it is edited, deleted, liked or warned about,
 * by anybody: an answer rewritten after it was read, or a word taken back once
 * the ticket was closed, breaks the record. A mistyped word is set right by
 * the next answer. The guest who filed a ticket signs his answers "Гость".
 *
 * The form (TicketAnswerForm) is there while the reader may write
 * (`canComment`, the server's answer); moderation chooses in it the status the
 * answer moves the ticket to (`canResolve`). Where it is not, one plate says
 * why, as every place the reader may not write in says it (RefusalNote): a
 * closed ticket says when it was closed and by whom, an open one takes no
 * answer past the hundredth and says where the next word goes. Nothing it may
 * not do is offered.
 *
 * It knows nothing of who reads: the author, a moderator and the guest are
 * one block. The page hands it the ticket, the sending, and what to say while
 * nobody has answered yet (the author is told he will be notified; the guest's
 * page says it in its own lead, and a moderator is told nothing).
 */
import { computed } from "vue";
import type { Comment, GeneralError } from "@/shared/api/models/common";
import { RefusalNote, SecondaryText } from "@/shared/ui/Layout";
import { useCommentHashScroll } from "@/shared/lib/composables/useScrollToElement";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { REMOVED_ACCOUNT_NAME } from "@/entities/user";
import type { AnswerStatus, TicketComment } from "@/entities/ticket";
import { CommentItem } from "@/features/comment";
import TicketAnswerForm from "./TicketAnswerForm.vue";
import type { ConversationTicket } from "./types";

/** What a mutation answers with: null when the server took the change. */
type SubmitResult = { error: GeneralError | null };

const props = defineProps<{
  ticket: ConversationTicket;
  /**
   * Posts an answer; the result decides whether the draft is dropped. The
   * page puts what it got back into the ticket it holds.
   */
  send: (text: string, status?: AnswerStatus) => Promise<SubmitResult>;
  /** The key the form keeps the unsent draft under. */
  draftKey: string;
  /** What to say while an open ticket has no answer yet; nothing if empty. */
  emptyText?: string;
}>();

const isOpen = computed(
  () =>
    props.ticket.status === "WaitingForModeration" ||
    props.ticket.status === "WaitingForUser",
);

/**
 * An answer of the conversation in the shape of a comment of the site: never
 * edited, never liked, never removed under the reader.
 */
function asComment(comment: TicketComment): Comment {
  return {
    id: comment.id as Comment["id"],
    author: (comment.author ?? null) as Comment["author"],
    createdUtc: comment.createdUtc as Comment["createdUtc"],
    modifiedUtc: null as Comment["modifiedUtc"],
    text: comment.text,
    isRemoved: false as Comment["isRemoved"],
    likes: [] as unknown as Comment["likes"],
  };
}

const answers = computed(() =>
  props.ticket.comments.map((comment) => ({
    comment: asComment(comment),
    guest: comment.isGuest,
  })),
);

/**
 * The line of a closed ticket: when and by whom. "(а)" with no gender, as the
 * records of a ban say "выдал(а)" and "снял(а)": the next screen of
 * moderation, and the same signature of what a moderator did. A closer whose
 * account was removed since is named as every such name is.
 */
const closedLine = computed(() => {
  if (isOpen.value) return null;
  const t = props.ticket;
  const how =
    t.status === "Spam" ? "Обращение закрыто как спам" : "Обращение закрыто";
  const when = t.resolvedUtc ? ` ${formatDateFull(t.resolvedUtc)}` : "";
  const who = t.resolvedByUsername ?? REMOVED_ACCOUNT_NAME;
  return `${how}${when}, закрыл(а) ${who}.`;
});

// The number of an answer is a link to it, and its address opens the ticket
// at that answer.
useCommentHashScroll(() => props.ticket.comments);
</script>

<template>
  <div class="ticket-conversation">
    <div v-if="answers.length" class="ticket-conversation__list">
      <CommentItem
        v-for="(item, index) in answers"
        :key="item.comment.id"
        :comment="item.comment"
        :guest="item.guest"
        :compact="true"
        :number="index + 1"
        noun="Ответ"
        readonly
      />
    </div>
    <SecondaryText v-else-if="isOpen && emptyText">{{
      emptyText
    }}</SecondaryText>

    <RefusalNote v-if="closedLine">{{ closedLine }}</RefusalNote>
    <TicketAnswerForm
      v-else-if="ticket.canComment"
      :send="send"
      :draft-key="draftKey"
      :chooses-status="!!ticket.canResolve"
    />
    <!-- An open ticket the reader may not write in: the conversation is full
         (TicketTextLimits.MaxComments on the server). -->
    <RefusalNote v-else
      >В обращении 100 ответов, для продолжения подайте новое
      обращение</RefusalNote
    >
  </div>
</template>

<style scoped lang="sass">
// The steps of the discussion of a topic (DiscussionSection): a $medium one
// under the block of the ticket and between the answers, the lines and the
// form, a $small one between two answers. The lines take no inset of their
// own: they stand where the form would and start where the column starts.
.ticket-conversation
  display: flex
  flex-direction: column
  gap: $medium
  margin-top: $medium

.ticket-conversation__list
  display: flex
  flex-direction: column
  gap: $small
</style>
