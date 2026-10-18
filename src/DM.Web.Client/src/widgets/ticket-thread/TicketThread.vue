<script setup lang="ts">
/**
 * One ticket ("обращение"), the same block in every place that reads a ticket
 * back: "Мои обращения" and the page of one ticket, the guest's page, the
 * queue of moderation and a moderator's page.
 *
 * A ticket works as a topic with answers and looks like one: the dashed
 * rectangle of a topic (RecordBlock has the frame, the ground and the heading
 * of TopicCard), headed by its reason as a topic is headed by its title, its
 * text under that, and under the text the caption of a topic, "Автор: имя
 * [online], дата | Ответы (N) | состояние". What moderation answered is an
 * answer of the conversation under the block (TicketConversation).
 *
 * The text is the author's and nothing else: what the form asked besides it
 * (whom the complaint is about, the address of the violation, the contact the
 * author left) the server wrote into it as lines of text at filing, so the
 * block draws no lines of its own over it and adds none to it, nothing grey
 * and nothing small that the markup could not write. It stands as the text of
 * a comment does, flush on both sides and hyphenated by the reader's switch
 * (RecordBlock).
 *
 * Nothing in the block has a colour of its own but one mark: a red "*" after
 * the state where the ticket waits for the reader's answer (`awaited`), the
 * star the menu of a game sets by a room that waits for the reader's move
 * (GameRoomLink). The letter of the role and [online] keep theirs, as by
 * every name of the site.
 *
 * The number of answers is a link to the ticket's page, as the number of
 * comments of a topic is. In a list the heading leads there as well and a
 * long text folds as a topic of a list does; on the ticket's own page the
 * text stands whole. The reader's own list ("Мои обращения") leaves the author
 * out of the caption: every ticket there is his.
 *
 * The address a complaint carries is a link (linkTicketAddress): the server
 * writes it into the text as a line of words, and a moderator had to copy it
 * into the address bar to see what the complaint was about.
 */
import { computed } from "vue";
import type { RouteLocationRaw } from "vue-router";
import dayjs from "dayjs";
import { RecordBlock } from "@/entities/moderation";
import {
  TICKET_STATUS_LABELS,
  TICKET_SUBTYPE_LABELS,
  linkTicketAddress,
} from "@/entities/ticket";
import { RemovedAccountLink } from "@/entities/user";
import { getRoleBadge } from "@/shared/config/roles";
import { ONLINE_THRESHOLD_MINUTES } from "@/shared/lib/constants/user";
import { CaptionPart } from "@/shared/ui/CaptionLine";
import { ContentText } from "@/shared/ui/Content";
import { Tooltip } from "@/shared/ui/Tooltip";
import { TruncatedContent } from "@/shared/ui/TruncatedContent";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import type { TicketView } from "./types";

const props = withDefaults(
  defineProps<{
    ticket: TicketView;
    /**
     * The caption names who wrote the ticket. Everywhere but the reader's own
     * list, where every ticket is his.
     */
    withAuthor?: boolean;
    /** In a list: where the heading leads. A long text folds there too. */
    to?: RouteLocationRaw | null;
    /**
     * Where the number of answers leads: the ticket's page. In a list it is
     * where the heading leads; the page of one ticket names itself.
     */
    answersTo?: RouteLocationRaw | null;
    /** Whether the ticket waits for the answer of the reader (awaitsReader). */
    awaited?: boolean;
  }>(),
  { withAuthor: true, to: null, answersTo: null, awaited: false },
);

/** The fold of a text in a list: a topic's in a list of topics (TopicCard). */
const LISTED_MAX_HEIGHT = 150;

/** The mark of a ticket that waits for the reader, GameRoomLink's. */
const STAR = "*";

const reason = computed(() => TICKET_SUBTYPE_LABELS[props.ticket.subtype]);

/** The text as the server rendered it, its address made a link. */
const bodyHtml = computed(() =>
  linkTicketAddress(props.ticket.descriptionHtml),
);

const reporter = computed(() => props.ticket.reporter ?? null);

/**
 * Whether a guest filed it. The wire says so in a flag, for a removed account
 * arrives without a reporter too. The guest's own page reads the ticket by its
 * token, and that wire (TrackedTicket) has no flag: every ticket read that way
 * is a guest's. The caption names him "Гость", the word his answers are
 * signed with under the block (CommentItem), written where a name stands as
 * "Аккаунт удален" is.
 */
const isGuest = computed(() => props.ticket.isGuest ?? true);

const roleBadge = computed(() => getRoleBadge(reporter.value?.role));

const isReporterOnline = computed(() => {
  const lastActivityUtc = reporter.value?.lastActivityUtc;
  if (!lastActivityUtc) return false;
  return (
    dayjs().diff(dayjs(lastActivityUtc), "minute", true) <=
    ONLINE_THRESHOLD_MINUTES
  );
});

const created = computed(() => formatDateFull(props.ticket.createdUtc));

/**
 * The answers the page holds, over the number the ticket was sent with: a
 * page puts the answer it sent into its conversation, and the number it was
 * sent is the one before that. A list holds no conversation, only the number.
 */
const answersCount = computed(
  () => props.ticket.comments?.length ?? props.ticket.commentsCount ?? 0,
);

const answersLink = computed(() => props.answersTo ?? props.to);

const stateLabel = computed(() => TICKET_STATUS_LABELS[props.ticket.status]);
</script>

<template>
  <article class="ticket-thread">
    <RecordBlock>
      <template #title>
        <router-link v-if="to" :to="to">{{ reason }}</router-link>
        <template v-else>{{ reason }}</template>
      </template>
      <TruncatedContent
        :truncatable="!!to"
        :max-height="LISTED_MAX_HEIGHT"
        :watch-key="ticket.descriptionHtml"
      >
        <ContentText class="ticket-body" :html="bodyHtml" />
      </TruncatedContent>
      <!-- The caption of a topic (TopicCard): the author and the date are two
           parts joined by a comma, the number of answers a link as the number
           of comments of a topic is, and the state last. -->
      <template #caption>
        <CaptionPart v-if="withAuthor" joint=","
          >Автор:
          <router-link
            v-if="reporter"
            :to="{ name: 'profile', params: { username: reporter.username } }"
            class="author-link"
            >{{ reporter.username }}</router-link
          ><template v-else-if="isGuest">Гость</template
          ><RemovedAccountLink v-else /><template v-if="roleBadge">
            [<Tooltip :text="roleBadge.label"
              ><b class="role-letter">{{ roleBadge.letter }}</b></Tooltip
            >]</template
          ><template v-if="reporter">
            [<span :class="isReporterOnline ? 'online' : 'offline'">{{
              isReporterOnline ? "online" : "offline"
            }}</span
            >]</template
          ></CaptionPart
        >
        <span>{{ created }}</span>
        <span
          >Ответы (<router-link
            v-if="answersLink"
            :to="answersLink"
            class="answers-link"
            >{{ answersCount }}</router-link
          ><template v-else>{{ answersCount }}</template
          >)</span
        >
        <span
          >{{ stateLabel
          }}<template v-if="awaited"
            >{{ " "
            }}<span class="star" aria-label="Ожидает вашего ответа">{{
              STAR
            }}</span></template
          ></span
        >
      </template>
    </RecordBlock>
  </article>
</template>

<style scoped lang="sass">
// The name of the author and the marks by it, as by the author of a topic
// (TopicCard): the link in the colour of a link, the letter of the role green
// and bold, [online] green and [offline] grey. The number of answers is a
// link in the colour of a link, as the number of comments of a topic.
.author-link,
.answers-link
  color: $link

  &:hover
    color: $link-hover

.role-letter
  font-weight: bold
  color: $accent-green
  cursor: help

.online
  color: $accent-green

.offline
  color: $text-muted

// The mark of a ticket that waits for the reader: the red of the star by a
// room that waits for his move (GameRoomLink, .star).
.star
  color: $accent-red
  cursor: default
</style>
