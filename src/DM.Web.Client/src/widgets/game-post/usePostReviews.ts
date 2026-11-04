/** The reviews of one post: their sum, their list under the card, and the viewer's form. */
import {
  computed,
  nextTick,
  onBeforeUnmount,
  onMounted,
  reactive,
  ref,
  watch,
  type Ref,
} from "vue";
import { storeToRefs } from "pinia";
import {
  useRoute,
  type RouteLocationNamedRaw,
  type RouteLocationRaw,
} from "vue-router";
import type { Post, PostReview } from "@/entities/game";
import { postApi, reviewSignName, reviewSignToNumber } from "@/entities/game";
import { unwrapResource } from "@/shared/api";
import { useAuthStore } from "@/shared/stores/auth";
import {
  registerExpandable,
  notifyExpandableChanged,
} from "@/shared/lib/composables/useExpandableRegistry";
import { scrollBlockIntoView } from "@/shared/lib/scroll";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

/** Games rated here since the posts were read: a rating closes its game to the rater. */
const gamesRatedHere = reactive(new Set<string>());

/** The sum and the count, kept true to what is done on the page until a refetch. */
function usePostReviewSum(post: () => Post) {
  const countOverride = ref<number | null>(null);
  const sumOverride = ref<number | null>(null);

  const count = computed(() => countOverride.value ?? post().reviewCount ?? 0);
  const sum = computed(() => sumOverride.value ?? post().rating ?? null);

  return { countOverride, sumOverride, count, sum };
}

/** The list under the card: fetched when first opened, a member of "Развернуть все". */
function usePostReviewList(
  postId: () => string | undefined,
  count: Ref<number>,
) {
  const open = ref(false);
  const list = ref<PostReview[]>([]);
  const loaded = ref(false);
  const loading = ref(false);
  const error = ref<string | null>(null);
  const hasReviews = computed(() => count.value > 0);

  async function load() {
    const id = postId();
    if (loaded.value || loading.value || !id) return;
    loading.value = true;
    error.value = null;
    // Every review in one request, not the default first 20.
    const take = Math.max(20, count.value);
    // Api never throws — failures come back in the error field
    const { data, error: failed } = await postApi.getPostReviews(id, { take });
    loading.value = false;
    if (failed) {
      error.value = "Не удалось загрузить оценки";
      return;
    }
    list.value = data?.resources ?? [];
    loaded.value = true;
  }

  const handleId = Symbol("post-reviews");
  let unregister: (() => void) | null = null;
  function ensureRegistration() {
    if (hasReviews.value && !unregister) {
      unregister = registerExpandable({
        id: handleId,
        isExpanded: () => open.value,
        // Pure state changes: a notify from a bulk expand would break the other handles.
        expand: () => {
          if (!open.value) {
            open.value = true;
            load();
          }
        },
        collapse: () => {
          if (open.value) open.value = false;
        },
      });
    }
  }
  // Registered in setup, so "Развернуть все" appears in the post's first frame.
  ensureRegistration();
  watch(hasReviews, ensureRegistration);
  onBeforeUnmount(() => unregister?.());

  /** Manual toggle by user click: notifies the registry so pendingAction resets. */
  async function toggle() {
    open.value = !open.value;
    if (open.value) await load();
    notifyExpandableChanged();
  }

  return { open, list, loading, error, hasReviews, load, toggle };
}

/** Who may write a review: the server's answer, narrowed by what happened on this page. */
function usePostReviewViewer(
  post: () => Post,
  list: Ref<PostReview[]>,
  isDeleted: () => boolean,
  isPostAuthor: () => boolean,
) {
  const authStore = useAuthStore();
  const { user: currentUser } = storeToRefs(authStore);

  const alreadyVoted = computed(
    () =>
      !!currentUser.value &&
      list.value.some(
        (r) => r.author?.username === currentUser.value?.username,
      ),
  );
  // Newbies are limited to the neutral "=" sign until they reach 100 posts.
  const canPickSigned = computed(() => !currentUser.value?.isNewbie);
  /** The answer for the viewer of the moment, once read again for them. */
  const viewerAnswer = ref<Pick<
    NonNullable<Post["viewer"]>,
    "canReview"
  > | null>(null);
  watch(post, () => {
    viewerAnswer.value = null;
  });
  const postViewer = computed(() => viewerAnswer.value ?? post().viewer);
  const gameRatedHere = computed(() => {
    const gameId = post().gameId;
    return !!gameId && gamesRatedHere.has(gameId);
  });
  // What opens the reviews of a post nobody rated: "+0" leads to the first form.
  const canReview = computed(
    () =>
      authStore.isAuthenticated &&
      !isDeleted() &&
      !!postViewer.value?.canReview &&
      !gameRatedHere.value,
  );

  /** Asks the server again; a failed read leaves the form off. */
  async function askAgain(username: string): Promise<boolean> {
    const id = post().id;
    if (!id) return false;
    const { data } = await postApi.getPost(id, OWNS_REFUSAL);
    // A post replaced or a viewer changed again meanwhile owns the answer.
    if (post().id !== id || currentUser.value?.username !== username) {
      return false;
    }
    const fresh = unwrapResource<Post>(data)?.viewer;
    if (!fresh) return false;
    viewerAnswer.value = fresh;
    return fresh.canReview;
  }

  // A sign-in or sign-out makes the answer somebody else's: no form until it is asked.
  watch(
    () => currentUser.value?.username ?? null,
    (username, before) => {
      if (username === before) return;
      gamesRatedHere.clear();
      viewerAnswer.value = { canReview: false };
      if (username && !isPostAuthor()) void askAgain(username);
    },
  );
  const showForm = computed(() => canReview.value && !alreadyVoted.value);

  return { currentUser, canPickSigned, canReview, showForm, askAgain };
}

/** The address of a review: the post's address plus ?review=, read back to open it. */
function usePostReviewLinks(
  postRoute: () => RouteLocationNamedRaw | null,
  postAnchor: () => string,
) {
  const route = useRoute();

  function routeOf(reviewId: string): RouteLocationRaw {
    const own = postRoute();
    return own
      ? { ...own, query: { review: reviewId } }
      : {
          // The room page itself: its query, the page number, has to survive.
          path: route.path,
          query: { ...route.query, review: reviewId },
          hash: postAnchor(),
        };
  }

  // The hash makes it this post's business: every post of a room reads one query.
  const anchoredId = computed(() => {
    if (route.hash !== postAnchor()) return null;
    const asked = route.query.review;
    return (Array.isArray(asked) ? asked[0] : asked) || null;
  });

  /** The review marked: the one the address links to. */
  function isHighlighted(review: PostReview): boolean {
    return review.id === anchoredId.value;
  }

  return { routeOf, anchoredId, isHighlighted };
}

/** The sum and the list kept true to what is done on the page. */
function usePostReviewChanges(
  post: () => Post,
  sum: ReturnType<typeof usePostReviewSum>,
  list: Ref<PostReview[]>,
  viewer: ReturnType<typeof usePostReviewViewer>,
) {
  /** A review written here: first, as the server lists and numbers them. */
  function onCreated(created: PostReview, sign: number) {
    list.value.unshift(created);
    sum.countOverride.value = sum.count.value + 1;
    sum.sumOverride.value = (sum.sum.value ?? 0) + sign;
    // The rating closes the game to this rater: its other posts stop offering the form.
    const gameId = post().gameId;
    if (gameId) gamesRatedHere.add(gameId);
  }

  /** A review changed sign: the sum moves by the difference. */
  function onUpdated({ id, sign }: { id: string; sign: number }) {
    const review = list.value.find((r) => r.id === id);
    if (!review) return;
    const previous = reviewSignToNumber(review.sign);
    if (previous !== sign) {
      sum.sumOverride.value = (sum.sum.value ?? 0) + (sign - previous);
    }
    // In the wire's spelling, so a second edit measures against a sign.
    review.sign = reviewSignName(sign);
  }

  /** A deleted review leaves the list, the count and the sum, as the server recomputes them. */
  function onDeleted({ id, sign }: { id: string; sign: number }) {
    const deleted = list.value.find((r) => r.id === id);
    if (!deleted) return;
    list.value = list.value.filter((r) => r.id !== id);
    sum.countOverride.value = Math.max(0, sum.count.value - 1);
    sum.sumOverride.value = (sum.sum.value ?? 0) - sign;
    // Whether the viewer may rate again is the server's to say.
    const username = viewer.currentUser.value?.username;
    if (username && deleted.author?.username === username) {
      void viewer.askAgain(username).then((reopened) => {
        const gameId = post().gameId;
        if (reopened && gameId) gamesRatedHere.delete(gameId);
      });
    }
  }

  return { onCreated, onUpdated, onDeleted };
}

/** What the post hands the model of its reviews. */
interface PostReviewsHost {
  post: () => Post;
  isDeleted: () => boolean;
  isPostAuthor: () => boolean;
  /** The post's own address, null on the room page itself. */
  postRoute: () => RouteLocationNamedRaw | null;
  postAnchor: () => string;
}

/** The model the sum and the list are drawn from. */
export function usePostReviews(host: PostReviewsHost) {
  const postId = () => host.post().id;
  const sum = usePostReviewSum(host.post);
  const reviews = usePostReviewList(postId, sum.count);
  const viewer = usePostReviewViewer(
    host.post,
    reviews.list,
    host.isDeleted,
    host.isPostAuthor,
  );
  const links = usePostReviewLinks(host.postRoute, host.postAnchor);
  const canOpen = computed(
    () => reviews.hasReviews.value || viewer.canReview.value,
  );

  // A permalink opens what it points at, once, on arrival; a post gone before
  // its reviews came back brings up nothing, whatever has the id by then.
  let gone = false;
  onBeforeUnmount(() => (gone = true));
  onMounted(async () => {
    const target = links.anchoredId.value;
    if (!target) return;
    reviews.open.value = true;
    await reviews.load();
    if (gone) return;
    await nextTick();
    if (gone) return;
    const marked = document.getElementById(`review-${target}`);
    if (marked) scrollBlockIntoView(marked);
  });

  const changes = usePostReviewChanges(host.post, sum, reviews.list, viewer);

  return reactive({
    postId: computed(postId),
    collapseId: computed(() => `post-reviews-${postId()}`),
    sum: sum.sum,
    open: reviews.open,
    list: reviews.list,
    loading: reviews.loading,
    error: reviews.error,
    hasReviews: reviews.hasReviews,
    canOpen,
    canPickSigned: viewer.canPickSigned,
    showForm: viewer.showForm,
    toggle: reviews.toggle,
    routeOf: links.routeOf,
    isHighlighted: links.isHighlighted,
    ...changes,
  });
}

/** The model of a post's reviews, as the sum and the list read it. */
export type PostReviews = ReturnType<typeof usePostReviews>;
