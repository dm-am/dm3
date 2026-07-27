import { gameApi } from "@/entities/game";

/** Room pages render 20 posts (gameApi.getPosts default page size). */
const POSTS_PAGE_SIZE = 20;

export type PostJumpTarget = {
  /** 1-based room ordinal, used as the game-room route `num` param. */
  roomNumber: number;
  /** Page within the room that contains the post. */
  page: number;
  /** Post id, passed as `scrollTo` for the room page to scroll/highlight. */
  postId: string;
};

/**
 * Resolve where a game-post search hit lives so it can be linked in context.
 *
 * The search endpoint returns only gameId + postId, so this reconstructs the
 * room ordinal and the page:
 *   1. GET the post to learn its room (roomNumber, id) and createdUtc.
 *   2. Binary-search the room's posts by skip/createdUtc to find the post's
 *      0-based index (rooms are ordered oldest-first, same as the room view
 *      and the first-unread math), then derive the page.
 *   3. Verify the candidate page actually contains the post (guards against
 *      same-timestamp ties landing it one page over) and nudge if needed.
 *
 * Returns null when the post or its room can't be resolved — callers fall
 * back to the game/rooms list.
 */
export async function resolvePostJump(
  postId: string,
): Promise<PostJumpTarget | null> {
  const { data: post } = await gameApi.getPost(postId);
  const room = post?.room;
  if (!post || !room?.id) return null;

  const roomId = room.id as unknown as string;
  const roomNumber = room.roomNumber as unknown as number;
  const targetCreatedUtc = post.createdUtc;

  // Total posts in the room — needed to bound the binary search.
  const { data: first } = await gameApi.getPosts(roomId, { skip: 0, take: 1 });
  const total = first?.paging?.total ?? 0;
  if (total <= 0) return { roomNumber, page: 1, postId };

  // Find the first index whose post is not older than the target.
  let lo = 0;
  let hi = total - 1;
  while (lo < hi) {
    const mid = (lo + hi) >> 1;
    const { data: probe } = await gameApi.getPosts(roomId, {
      skip: mid,
      take: 1,
    });
    const probePost = probe?.resources?.[0];
    if (probePost && probePost.createdUtc < targetCreatedUtc) {
      lo = mid + 1;
    } else {
      hi = mid;
    }
  }
  let page = Math.floor(lo / POSTS_PAGE_SIZE) + 1;

  // Verify the exact post is on the derived page; if a timestamp tie pushed it
  // over, scan a couple of neighbouring pages before giving up on precision.
  const totalPages = Math.ceil(total / POSTS_PAGE_SIZE);
  for (const candidate of [page, page + 1, page - 1]) {
    if (candidate < 1 || candidate > totalPages) continue;
    const { data } = await gameApi.getPosts(roomId, { number: candidate });
    const found = data?.resources?.some(
      (p) => (p.id as unknown as string) === postId,
    );
    if (found) {
      page = candidate;
      break;
    }
  }

  return { roomNumber, page, postId };
}
