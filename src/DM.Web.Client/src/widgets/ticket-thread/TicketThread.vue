<script setup lang="ts">
/** One ticket, the same block wherever a ticket is read: a topic's block and caption. */
import { computed } from "vue";
import type { RouteLocationRaw } from "vue-router";
import { RecordBlock } from "@/entities/moderation";
import {
  TICKET_STATUS_LABELS,
  TICKET_SUBTYPE_LABELS,
  linkTicketAddress,
} from "@/entities/ticket";
import { RemovedAccountLink, UserRoleBadge } from "@/entities/user";
import { getRoleBadge } from "@/shared/config/roles";
import { CaptionPart } from "@/shared/ui/CaptionLine";
import { ContentText } from "@/shared/ui/Content";
import { TruncatedContent } from "@/shared/ui/TruncatedContent";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { isUserOnline } from "@/shared/lib/utils/chat";
import type { TicketView } from "./types";

const props = withDefaults(
  defineProps<{
    ticket: TicketView;
    /** The caption names who wrote it, but in the reader's own list. */
    withAuthor?: boolean;
    /** In a list: where the heading leads. A long text folds there too. */
    to?: RouteLocationRaw | null;
    /** Where the number of answers leads: the ticket's page. */
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
const bodyHtml = computed(() => linkTicketAddress(props.ticket.description));

const reporter = computed(() => props.ticket.reporter ?? null);

/** A guest filed it: the flag tells one from a removed account; a ticket read by token has none and is a guest's. */
const isGuest = computed(() => props.ticket.isGuest ?? true);

const roleBadge = computed(() => getRoleBadge(reporter.value?.role));

const isReporterOnline = computed(() =>
  isUserOnline(reporter.value?.lastActivityUtc),
);

const created = computed(() => formatDateFull(props.ticket.createdUtc));

/** The answers the page holds over the number sent: the page appends the answer it sent. */
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
        :watch-key="ticket.description"
      >
        <ContentText class="ticket-body" :html="bodyHtml" />
      </TruncatedContent>
      <!-- The caption of a topic: the author and the date joined by a comma, the state last -->
      <template #caption>
        <CaptionPart v-if="withAuthor" joint=","
          >Автор:
          <router-link
            v-if="reporter"
            :to="{ name: 'profile', params: { username: reporter.username } }"
            class="author-link"
            >{{ reporter.username }}</router-link
          ><template v-else-if="isGuest">Гость</template
          ><RemovedAccountLink v-else /><UserRoleBadge
            v-if="roleBadge"
            :badge="roleBadge"
            variant="caption"
          /><template v-if="reporter">
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
// The mark by the author's name, as by the author of a topic.
.online
  color: $accent-green

.offline
  color: $text-muted

// The red of the star by a room that waits for the reader's move.
.star
  color: $accent-red
  cursor: default
</style>
