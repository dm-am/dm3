<script setup lang="ts">
/**
 * ModerationRatedPosts — "Последние оцененные посты" (doc 4.2.3.8.4).
 * The cross-game worklist of posts that received reviews, newest review
 * first. The list is widgets/rated-posts, the same one the game page and the
 * profile subpages draw; this page only opens it on the whole site and gates
 * it by role. It used to fetch one fixed page of fifty with no filter and no
 * paging, so a worklist longer than fifty silently ended there.
 */
import type { RatedPostsScope } from "@/entities/game";
import { RatedPostsList } from "@/widgets/rated-posts";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { useRoleGate } from "./lib/useRoleGate";

const { hasAccess, deniedText } = useRoleGate("Moderator");

const scope: RatedPostsScope = { kind: "site" };
const pagingTo = { name: "moderation-rated-posts" as const };
</script>

<template>
  <div class="moderation-rated-posts">
    <page-title>Последние оцененные посты</page-title>

    <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

    <RatedPostsList
      v-else
      :scope="scope"
      :paging-to="pagingTo"
      empty-text="Оцененных постов пока нет"
    />
  </div>
</template>
