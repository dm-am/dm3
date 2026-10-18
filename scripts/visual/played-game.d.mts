// Types of played-game.mjs for the TypeScript side of the stand.
export declare function findPlayedGame(
  get: (path: string) => Promise<any>,
  member: string,
): Promise<{
  game: string;
  characterId: string;
  roomId: string;
  ownPostCreatedUtc: string;
  chatRoomId: string;
  chatRoomNumber: number;
}>;
