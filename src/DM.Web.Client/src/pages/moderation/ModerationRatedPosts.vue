<script setup lang="ts">
import type { RatedPostsScope } from "@/entities/game";
import { RatedPostsList } from "@/widgets/rated-posts";

const scope: RatedPostsScope = { kind: "site" };
const pagingTo = { name: "moderation-rated-posts" as const };
</script>

<template>
  <div class="moderation-rated-posts">
    <page-title>Последние оцененные посты</page-title>

    <RatedPostsList
      :scope="scope"
      :paging-to="pagingTo"
      empty-text="Оцененных постов пока нет"
    />
  </div>
</template>
