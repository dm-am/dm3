/**
 * Cross-import surface for features/create-game (FSD @x).
 *
 * Creating a game embeds the attribute-schema editor and opens it on the site's
 * default sheet, so the source list travels through the same door: the pure
 * schema helpers both sides use live in entities/game, where a page reaches
 * them by importing downwards.
 */
export { AttributeSchemaEditor, useSchemaSources } from "..";
