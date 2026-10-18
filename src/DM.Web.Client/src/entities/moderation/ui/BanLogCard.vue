<script setup lang="ts">
/**
 * One entry of the ban list, written as DM2's bot wrote its ban log (DM2's
 * topic "Бан-лог"), so a reader of that log reads this one the same way.
 *
 * The bot wrote every issue and every lift as a comment of one pattern, the
 * same in 1886 entries, and the sentence here is its sentence: the moderator,
 * "выдал(а)" or "снял(а)", the scope of the ban, "пользователю" or "с
 * пользователя", the login, "до" and the end date in bold, a full stop. The end
 * of a lifted ban is the end it had, not the day of the lift. A permanent ban
 * is said in a word, "бессрочный", with no "до": DM2 printed a date five
 * hundred years away, and ours would be a hundred, a number nobody knows.
 *
 * Where the entry says more than DM2's did, it says it for the reader. The
 * moderator is a bold link to his profile, as the login is: whose decision it
 * was is the first thing a reader of the log looks up. The reason stands under
 * "Примечание от модератора:" on a line of its own, drawn as the [quote] of
 * the site's text, so the moderator's words stand apart from the sentence the
 * site writes; an entry without a reason keeps the heading over nothing, as
 * DM2 kept its "Комментарий:". The caption says "Выдан:" or "Снят:" before the
 * moment, so the moment says what it is the moment of. The sentence is the
 * text of a record, set as a comment's text is (RecordBlock): justified, its
 * words broken while "Перенос слов" is on.
 *
 * In the corner of the caption line stands the number of the entry in the
 * whole list, a grey link to the entry, as the number of a comment is
 * (CommentItem): DM2 numbered the comments of its ban log the same way. The
 * page says where the link leads (`numberTo`); the number is the server's, the
 * same on every page and in the list of one person.
 *
 * Nothing in the entry is red or green: a ban is the record of a decision, and
 * DM2 coloured nothing in its log but the logins. The lift is offered where the
 * server says this reader may lift this ban (`canLift`, worked out by the rule
 * that refuses the lift itself), and only on the entry of the issue; the card
 * asks no server, the caller opens the window. The reason of a ban arrives
 * rendered, written in the BBCode editor; the reason of a lift is a plain
 * field and stays text.
 */
import { computed } from "vue";
import type { RouteLocationRaw } from "vue-router";
import { RemovedAccountLink } from "@/entities/user/@x/moderation";
import { CaptionAction, CaptionLine } from "@/shared/ui/CaptionLine";
import { formatDate, formatDateFull } from "@/shared/lib/utils/datetime";
import type { BanLogEntry } from "../api/moderationApi";
import { banScopeLabel, isPermanent } from "../lib/warningLabels";
import RecordBlock from "./RecordBlock.vue";

// The attributes the list gives the entry, its anchor first, go to the record:
// the record carries the ground, and the mark of an entry the link arrived at
// is drawn on the ground, as on a comment.
defineOptions({ inheritAttrs: false });

const props = defineProps<{
  entry: BanLogEntry;
  /** Where the number of the entry leads: the entry, on its page of the list. */
  numberTo?: RouteLocationRaw;
}>();

/** "до" and the date never part at the end of a line. */
const NBSP = "\u00a0";

const emit = defineEmits<{
  /** The lift was asked for, on the ban of this entry. */
  (e: "lift"): void;
}>();

const ban = computed(() => props.entry.ban);
const isLift = computed(() => props.entry.kind === "Lifted");

/** Who did it: the moderator of the ban, or the one who lifted it. */
const actor = computed(() =>
  isLift.value ? ban.value.liftedBy : ban.value.moderator,
);

const scope = computed(() => banScopeLabel(ban.value));

/** The end the ban had; none for a permanent one, which says so in a word. */
const end = computed(() =>
  isPermanent(ban.value) ? null : formatDate(ban.value.expiresUtc),
);

/**
 * The reason, rendered for a ban and typed for a lift. An entry without one,
 * which DM2's log has, keeps the heading of the note and draws no quote under
 * it: an empty quote would say there is something to read.
 */
const reason = computed(() =>
  isLift.value ? ban.value.liftReason?.trim() : ban.value.comment?.trim(),
);

const canLift = computed(() => !isLift.value && ban.value.canLift);
</script>

<template>
  <article class="ban-log-card">
    <RecordBlock v-bind="$attrs">
      <!-- The sentence, a blank line and the note, one run of text as DM2
           wrote it. The spaces are written out, so the copied text reads as
           the shown one. -->
      <span class="ban-log-card__phrase"
        ><b class="ban-log-card__user"
          ><router-link
            v-if="actor"
            :to="{ name: 'profile', params: { username: actor.username } }"
            >{{ actor.username }}</router-link
          ><RemovedAccountLink v-else /></b
        >{{ ` ${isLift ? "снял(а)" : "выдал(а)"} ${scope} `
        }}{{ `${isLift ? "с пользователя" : "пользователю"} `
        }}<b class="ban-log-card__user"
          ><router-link
            v-if="ban.user"
            :to="{ name: 'profile', params: { username: ban.user.username } }"
            >{{ ban.user.username }}</router-link
          ><RemovedAccountLink v-else /></b
        ><template v-if="end"
          >{{ ` до${NBSP}` }}<b>{{ end }}</b></template
        >.</span
      ><br /><br /><span class="ban-log-card__note-label"
        >Примечание от модератора:</span
      >
      <div v-if="reason" class="ban-log-card__note bbcode-content">
        <div v-if="isLift" class="quote ban-log-card__lift-reason">
          {{ reason }}
        </div>
        <div v-else class="quote" v-html="ban.comment" />
      </div>

      <!-- The caption line and the number in its corner, as the footer of a
           comment stands (CommentItem): the line wraps inside itself and the
           number keeps the corner, on the baseline of the line's last row. -->
      <div class="ban-log-card__footer">
        <CaptionLine class="ban-log-card__caption">
          <span
            >{{ isLift ? "Снят" : "Выдан" }}:
            {{ formatDateFull(entry.occurredUtc) }}</span
          >
          <CaptionAction v-if="canLift" @click="emit('lift')"
            >Снять бан</CaptionAction
          >
        </CaptionLine>
        <!-- A number alone does not say what it is to a screen reader, hence
             the name; the router marks every link to this page as the current
             one, and none of these is. -->
        <router-link
          v-if="numberTo"
          :to="numberTo"
          class="ban-log-card__number"
          :aria-label="`Запись ${entry.number}`"
          :aria-current="undefined"
          >{{ entry.number }}</router-link
        >
      </div>
    </RecordBlock>
  </article>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

// DM2's bold: the moderator, the login and the end date, in the colour of the
// text; the two names keep the colour of a link, the one colour of the entry.
.ban-log-card__phrase b
  font-weight: bold
  color: $text

.ban-log-card__user :deep(a)
  color: $link

// The reason of a lift is typed in a field of several lines, and its lines stay
// as the moderator broke them; the rendered reason of a ban carries its own.
.ban-log-card__lift-reason
  white-space: pre-line

// The footer is a caption, not a part of the text it stands in: the caption's
// size, colour and line (RecordBlock, .record-block__caption), flush left, its
// words whole. A small step under the text, as the caption of every record.
.ban-log-card__footer
  display: flex
  flex-wrap: nowrap
  align-items: last baseline
  gap: $small
  margin-top: $small
  font-size: $tertiary-font-size
  line-height: 1.3
  text-align: left
  hyphens: manual
  -webkit-hyphens: manual

// The line takes the row and wraps inside itself.
.ban-log-card__caption
  flex: 1 1 0%
  min-width: 0

// The number of a comment (CommentItem, .comment-number): a grey link of the
// caption's size, pinned to the right edge by its auto margin.
.ban-log-card__number
  flex: none
  margin-left: auto
  font-size: $tertiary-font-size
  +muted-link
</style>
