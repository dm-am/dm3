<script setup lang="ts">
// The sentence of DM2's ban log bot, so a reader of that log reads this one the same way.
import { computed } from "vue";
import type { RouteLocationRaw } from "vue-router";
import { RemovedAccountLink } from "@/entities/user/@x/moderation";
import { CaptionAction, CaptionLine } from "@/shared/ui/CaptionLine";
import { formatDate, formatDateFull } from "@/shared/lib/utils/datetime";
import type { BanLogEntry } from "../api/banApi";
import { banScopeLabel, isPermanent } from "../lib/warningLabels";
import RecordBlock from "./RecordBlock.vue";

// The anchor goes to the record, which carries the ground the arrival mark is drawn on.
defineOptions({ inheritAttrs: false });

const props = defineProps<{
  entry: BanLogEntry;
  numberTo?: RouteLocationRaw;
}>();

/** "до" and the date never part at the end of a line. */
const NBSP = "\u00a0";

const emit = defineEmits<{
  (e: "lift"): void;
}>();

const ban = computed(() => props.entry.ban);
const isLift = computed(() => props.entry.kind === "Lifted");

const actor = computed(() =>
  isLift.value ? ban.value.liftedBy : ban.value.moderator,
);

const scope = computed(() => banScopeLabel(ban.value));

/** The end the ban had, also for a lifted one. */
const end = computed(() =>
  isPermanent(ban.value) ? null : formatDate(ban.value.expiresUtc),
);

// Rendered for a ban, typed for a lift; without one the heading stays over no quote.
const reason = computed(() =>
  isLift.value ? ban.value.liftReason?.trim() : ban.value.comment?.trim(),
);

const canLift = computed(() => !isLift.value && ban.value.canLift);
</script>

<template>
  <article class="ban-log-card">
    <RecordBlock v-bind="$attrs">
      <!-- The spaces are written out, so the copied text reads as the shown one. -->
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
        <!-- The router marks every link to this page as current, and none of these is. -->
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
@use "@/assets/styles/Links" as *

.ban-log-card__phrase b
  font-weight: bold
  color: $text

.ban-log-card__user :deep(a)
  color: $link

// The typed reason keeps the lines the moderator broke it into.
.ban-log-card__lift-reason
  white-space: pre-line

// A caption, not a part of the justified text it stands in.
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

.ban-log-card__caption
  flex: 1 1 0%
  min-width: 0

// As the number of a comment, CommentItem.
.ban-log-card__number
  flex: none
  margin-left: auto
  font-size: $tertiary-font-size
  +muted-link
</style>
