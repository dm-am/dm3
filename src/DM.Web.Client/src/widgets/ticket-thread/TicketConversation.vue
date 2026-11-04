<script setup lang="ts">
/** The conversation of a ticket: read-only answers, the record of how it was settled, and the form. */
import { computed } from "vue";
import type { Comment, SubmitResult } from "@/shared/api/models/common";
import { asServed } from "@/shared/api/models";
import { RefusalNote, SecondaryText } from "@/shared/ui/Layout";
import { useCommentHashScroll } from "@/shared/lib/composables/useScrollToElement";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { REMOVED_ACCOUNT_NAME } from "@/entities/user";
import type { AnswerStatus, TicketComment } from "@/entities/ticket";
import { CommentItem } from "@/features/comment";
import TicketAnswerForm from "./TicketAnswerForm.vue";
import type { ConversationTicket } from "./types";

const props = defineProps<{
  ticket: ConversationTicket;
  /** Posts an answer; the page puts what it got back into its ticket. */
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

/** An answer in the shape of a comment of the site: never edited, liked or removed. */
function asComment(comment: TicketComment): Comment {
  return {
    id: asServed(comment.id),
    author: asServed(comment.author ?? null),
    createdUtc: asServed(comment.createdUtc),
    modifiedUtc: null,
    text: comment.text,
    isRemoved: asServed(false),
    likes: asServed([]),
  };
}

const answers = computed(() =>
  props.ticket.comments.map((comment) => ({
    comment: asComment(comment),
    guest: comment.isGuest,
  })),
);

/** The line of a closed ticket: when and by whom, "(а)" with no gender as in the records of a ban. */
const closedLine = computed(() => {
  if (isOpen.value) return null;
  const t = props.ticket;
  const how =
    t.status === "Spam" ? "Обращение закрыто как спам" : "Обращение закрыто";
  const when = t.resolvedUtc ? ` ${formatDateFull(t.resolvedUtc)}` : "";
  const who = t.resolvedByUsername ?? REMOVED_ACCOUNT_NAME;
  return `${how}${when}, закрыл(а) ${who}.`;
});

// The number of an answer links to it, and its address opens the ticket there.
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
    <!-- No form for a reader the server does not let write, a full conversation among them -->
    <TicketAnswerForm
      v-else-if="ticket.canComment"
      :send="send"
      :draft-key="draftKey"
      :chooses-status="!!ticket.canResolve"
    />
  </div>
</template>

<style scoped lang="sass">
// The steps of a topic's discussion: $medium between the parts, $small between two answers.
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
