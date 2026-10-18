<script setup lang="ts">
/**
 * One warning, drawn the same way wherever it is read.
 *
 * Two screens show a warning: the open log, which everyone can read, and the
 * moderation list, which adds the control that changes the points. They used
 * to be one screen, so the card lived inside it; the second reader arrived
 * with the open log, and a copy of the block would have been a second place
 * for the wording of a warning to be decided.
 *
 * The card is two parts, both taken from the site (the owner, 28.09). On top,
 * the content the warning was issued for, in the dashed square of a comment,
 * with DM2's caption of content under it: "Название места, Автор: имя, дата".
 * Under it, the decisions as the reviews of a post: the warning, and the
 * change of its points when there was one, each signed "<Что> от <кого>,
 * <когда>" (warningLabels.ts). The points stand in that kind, the warning with
 * the points it was issued with, "Предупреждение (3 балла)", the change with
 * the points it left, "Изменение баллов (1 балл)", and not on the content (the
 * owner, 05.10: "У самого контента их незачем писать").
 *
 * The kind is red where it names points and grey where it names none: the
 * warning by the points it was issued with, the change by the points it left.
 * So a verbal warning is grey, a change to nothing is grey, and a warning of
 * 2 points stays red over the change that took them off, since its line still
 * says "(2 балла)". Nothing here is green: green says a thing is good, and a
 * warning never is (the owner, 12.10: "Давай серое при изменении на 0 или
 * устном. Если баллы есть, то красное").
 *
 * The text is always there, rendered by the server, a hidden block erased for
 * everybody (the owner, 05.10: "Всегда показывай"). It is the text the warning
 * was issued for, the copy the server took then, and the caption says nothing
 * of an edit made to it on the site afterwards (the owner, 12.10:
 * "Предупреждение же не к отредактированному. Тут везде надо то, к чему
 * предупреждение"). The place links to the text only where the server gives
 * the address, which is where a guest could follow it.
 *
 * A warning on a profile shows the profile the same way, as the server copied
 * it at the moment of issue (the owner, 12.10, line 41: "почему профиль
 * пользователя не видно?"), drawn as the profile page draws the same parts:
 * the avatar of 220 in its own proportions and not rounded, the status row,
 * and "О себе" as the heading the profile gives it, with the text under it,
 * in a column at every width; a part the profile did not have is not there,
 * and a removed account has none of it. Its caption is
 * DM2's "Профиль пользователя", leading to the profile. A warning that names
 * nothing at all, one issued on a ticket about a person, says whom it is about,
 * "Пользователь: имя": it may be about the private messages, and "Профиль
 * пользователя" over nothing is what made the owner ask where the profile was.
 *
 * The reason arrives rendered: the warning window takes it in the BBCode
 * editor. The explanation of a change of the points is a plain field.
 *
 * Two slots are for controls, on the contract the review keeps: the item signs
 * itself, and the caller adds its action at the end of a signature, of the
 * decision ("controls") or of the record ("record-controls").
 */
import { computed } from "vue";
import type { UserRef } from "@/shared/api/models/common";
import { UserLink } from "@/entities/user/@x/moderation";
import { AvatarImg } from "@/shared/ui/AvatarImg";
import { CaptionPart } from "@/shared/ui/CaptionLine";
import { TruncatedContent } from "@/shared/ui/TruncatedContent";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import {
  initBbcodeInteractive,
  trimHtmlWhitespace,
} from "@/shared/lib/utils/bbcodeInteractive";
import type { WarningPlaceKind, WarningProfile } from "../api/moderationApi";
import {
  placeLabel,
  pointsChangeLabel,
  warningKindLabel,
} from "../lib/warningLabels";
import RecordBlock from "./RecordBlock.vue";
import RecordVerdict from "./RecordVerdict.vue";
import RecordVerdictList from "./RecordVerdictList.vue";

/**
 * The union of what the two endpoints return: the open log entry and the
 * moderation row. Both server shapes satisfy it, so neither caller converts
 * anything on the way in.
 */
type WarningCardModel = {
  id: string;
  user?: UserRef;
  moderator?: UserRef;
  points: number;
  originalPoints: number;
  pointsChangedBy?: string | null;
  pointsChangedUtc?: string | null;
  pointsChangeText?: string | null;
  reason: string;
  createdUtc: string;
  entityType?: string | null;
  evidence?: string | null;
  entityUrl?: string | null;
  entityCreatedUtc?: string | null;
  placeKind?: WarningPlaceKind | null;
  placeTitle?: string | null;
  profile?: WarningProfile | null;
};

const props = defineProps<{ warning: WarningCardModel }>();

/**
 * The height past which the text folds, the comment's own (CommentItem.vue):
 * a warning quotes a comment, and it folds where the comment did.
 */
const EVIDENCE_MAX_HEIGHT = 300;

const evidenceHtml = computed(() =>
  trimHtmlWhitespace(props.warning.evidence ?? ""),
);

/** A warning on the profile of the warned user. */
const onProfile = computed(() => props.warning.entityType === "Profile");

/**
 * A warning that names nothing: no text and no place, issued on a ticket about
 * a person. The open log sends no kind for it and the moderation row
 * "Unknown"; a text whose copy and place are both gone reads the same.
 */
const namesNothing = computed(
  () =>
    !onProfile.value &&
    (!props.warning.entityType ||
      props.warning.entityType === "Unknown" ||
      (!props.warning.placeKind && !props.warning.evidence)),
);

/** The copy of the profile, when the server kept anything of it. */
const snapshot = computed(() => {
  const profile = onProfile.value ? props.warning.profile : null;
  return profile && (profile.picture || profile.status || profile.info)
    ? profile
    : null;
});

const aboutHtml = computed(() =>
  trimHtmlWhitespace(snapshot.value?.info ?? ""),
);

/** The name of the place: the topic, the game, the blog, "Чат". */
const place = computed(() =>
  onProfile.value
    ? "Профиль пользователя"
    : placeLabel(props.warning.placeKind, props.warning.placeTitle),
);

/** Where the name of the place leads: the text itself, or the profile. */
const placeTo = computed(() => {
  if (onProfile.value) {
    return props.warning.user
      ? { name: "profile", params: { username: props.warning.user.username } }
      : null;
  }
  return props.warning.entityUrl ?? null;
});

/** When the text was written, the date with its time. */
const createdDate = computed(() =>
  formatDateFull(props.warning.entityCreatedUtc),
);

/**
 * Red where the kind names points, grey where it names none: the warning by
 * the points it was issued with, the change by the points it left.
 */
const toneOf = (points: number) => (points > 0 ? "negative" : "neutral");
</script>

<template>
  <article class="warning-card">
    <RecordBlock>
      <!-- The text as its place shows it: rendered by the server, spoilers
           shut, folded where a comment folds; or the profile as it stood. A
           conditional slot, so a row with neither has no text line at all,
           not an empty one. -->
      <template v-if="warning.evidence || snapshot" #default>
        <TruncatedContent
          v-if="warning.evidence"
          :truncatable="true"
          :max-height="EVIDENCE_MAX_HEIGHT"
          :watch-key="evidenceHtml"
          :on-content-mounted="initBbcodeInteractive"
        >
          <div class="warning-evidence bbcode-content" v-html="evidenceHtml" />
        </TruncatedContent>
        <!-- The parts of the profile page (ProfilePage.vue), in its order:
             the avatar of 220 in its own proportions, the status row, "О себе"
             as its heading and the text under it, folded where a comment
             folds. No silhouette for a profile that had no avatar. -->
        <div v-else-if="snapshot" class="warning-profile">
          <AvatarImg
            v-if="snapshot.picture"
            :picture="snapshot.picture"
            :alt="warning.user?.username ?? ''"
            :size="220"
            img-class="warning-profile__picture"
            prefer-original
            no-default
          />
          <div v-if="snapshot.status" class="warning-profile__status">
            Статус:{{ " "
            }}<span class="warning-profile__status-value">{{
              snapshot.status
            }}</span>
          </div>
          <div v-if="snapshot.info" class="warning-profile__info">
            <h3 class="warning-profile__about-label">О себе</h3>
            <TruncatedContent
              :truncatable="true"
              :max-height="EVIDENCE_MAX_HEIGHT"
              :watch-key="aboutHtml"
              :on-content-mounted="initBbcodeInteractive"
            >
              <div
                class="warning-profile__about bbcode-content"
                v-html="aboutHtml"
              />
            </TruncatedContent>
          </div>
        </div>
      </template>
      <template #caption>
        <!-- Wraps between its parts at every width, as the caption of a
             topic: the place, the author and the date with its time each a
             part, the comma ending the part before it, so a narrow record
             breaks after a comma and takes the next part to a line of its
             own whole. Nothing is put away for want of room. -->
        <span v-if="namesNothing" class="warning-user"
          >Пользователь: <UserLink v-if="warning.user" :user="warning.user"
        /></span>
        <template v-else>
          <CaptionPart v-if="place" joint="," class="warning-place"
            ><router-link v-if="placeTo" :to="placeTo">{{ place }}</router-link
            ><template v-else>{{ place }}</template></CaptionPart
          >
          <CaptionPart
            v-if="warning.user"
            :joint="warning.entityCreatedUtc ? ',' : undefined"
            class="warning-author"
            >Автор: <UserLink :user="warning.user"
          /></CaptionPart>
          <span v-if="warning.entityCreatedUtc" class="warning-date">{{
            createdDate
          }}</span>
        </template>
        <slot name="record-controls" />
      </template>
    </RecordBlock>

    <RecordVerdictList>
      <RecordVerdict
        class="warning-verdict"
        :kind="warningKindLabel(warning.originalPoints)"
        :tone="toneOf(warning.originalPoints)"
        :author="warning.moderator?.username"
        :date="warning.createdUtc"
      >
        <span class="warning-reason bbcode-content" v-html="warning.reason" />
        <template #controls><slot name="controls" /></template>
      </RecordVerdict>

      <!-- The change of the points, a decision of its own under the warning:
           present only when the points were changed. -->
      <RecordVerdict
        v-if="warning.pointsChangedUtc"
        class="warning-change"
        :kind="pointsChangeLabel(warning.points)"
        :tone="toneOf(warning.points)"
        :author="warning.pointsChangedBy"
        :date="warning.pointsChangedUtc"
      >
        <span v-if="warning.pointsChangeText" class="warning-change-text">{{
          warning.pointsChangeText
        }}</span>
      </RecordVerdict>
    </RecordVerdictList>
  </article>
</template>

<style scoped lang="sass">
// The parts of the profile one under another, each at its own width on the
// left, as the column of the profile stands them (ProfilePage.vue,
// .col-identity).
.warning-profile
  display: flex
  flex-direction: column
  align-items: flex-start

// The avatar of the profile: 220 wide, no wider than the record, its own
// proportions, the frame and ground of the profile's, and no rounding. The
// profile puts 16 under it, its wrapper's 8 and the column's 8.
.warning-profile__picture
  display: block
  width: 220px
  max-width: 100%
  height: auto
  border: 1px solid $border
  background-color: $bg-element

  & + *
    margin-top: $medium

// The status row of the profile (.status-row): the text's size and colour on
// a leading of 1.5, flush left.
.warning-profile__status
  font-size: $font-size
  line-height: 1.5
  color: $text
  text-align: left

.warning-profile__status-value
  word-break: break-word

// The words about the person take the column's width, as the tab under the
// heading takes the page's.
.warning-profile__info
  align-self: stretch

  // The profile's "Информация" stands 24 under the block above it: its own
  // 16 and the page's 8.
  &:not(:first-child)
    margin-top: $medium + $small

// "О себе" as the profile heads it: its tab of headings (Tabs.vue,
// .tabs--headings .tab.active), the title's size in bold capitals, spaced,
// in the colour of headings, 16 over the text as the profile keeps it.
.warning-profile__about-label
  margin: 0 0 $medium
  font-size: $title-font-size
  font-weight: bold
  line-height: normal
  text-transform: uppercase
  letter-spacing: 0.5px
  color: $heading
  text-align: left
</style>
