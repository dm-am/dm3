/**
 * Result of finding the first unread post in a game
 */
export interface FirstUnreadPostResult {
  /** Room containing the first unread post */
  roomId: string;
  /** Post number (1-based) for pagination */
  postNumber: number;
  /** Post identifier for scroll targeting */
  postId: string;
  /** Total number of unread posts in the game */
  totalUnreadCount: number;
  /** Whether there are unread posts (or any posts for anonymous users) */
  hasUnread: boolean;
}

/**
 * Result of finding the first unread comment in a game
 */
export interface FirstUnreadCommentResult {
  /** Comment number (1-based) for pagination */
  commentNumber: number;
  /** Comment identifier for scroll targeting */
  commentId: string;
  /** Total number of unread comments in the game */
  totalUnreadCount: number;
  /** Whether there are unread comments (or any comments for anonymous users) */
  hasUnread: boolean;
}
