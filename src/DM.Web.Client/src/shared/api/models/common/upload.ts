/**
 * A processed upload. The thumbnails of an avatar come in the picture object of
 * the User, UserProfile and Character DTOs.
 */
export interface Upload {
  id: string;
  userId: string;
  /** On the moderation lists, for the "Загрузил" link; absent on self paths. */
  uploaderUsername?: string;
  type: string;
  targetId?: string;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  status: "Pending" | "Confirmed" | "Failed";
  /** Only avatars and portraits have one: the bucket reads anonymously there only. */
  url?: string;
  /** Serves every upload; the server checks the caller's right on each request. */
  contentUrl: string;
  createdUtc: string;
  confirmedUtc?: string;
  /** False for a file of a post kept from the caller: a list names it without a link or a preview. */
  canOpen: boolean;
  /** "Получатели:" of a file kept to the characters chosen; absent on a file every reader sees and when canOpen is false. */
  recipients?: string;
  /** The characters chosen; absent when recipients is. */
  addressees?: UploadAddressee[];
}

/** A character a file of a post is kept to, by his name when he was chosen: the room may no longer have him. */
export interface UploadAddressee {
  id: string;
  name: string;
}

/** Who sees a file of a post: "видят все" or "видят: Вася" in its line. */
type UploadVisibleTo = "All" | "Selected";

/** The mark of a file of a post; the characters count for "Selected" only. */
export interface UploadAudience {
  visibleTo: UploadVisibleTo;
  characterIds: string[];
}
