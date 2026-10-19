// A template is only a copy source: a game keeps its own sheet afterwards.
import type { AttributeSpecification } from "@/shared/api/models/game/attributeSchemas";

export interface AttributeSchemaTemplate {
  id: string;
  title: string;
  /** Whether the creation form starts from it; moved by an action of its own. */
  isDefault: boolean;
  specifications: AttributeSpecification[];
}

/** The writable part of a template, for creation and replacement alike. */
export interface AttributeSchemaTemplateRequest {
  title: string;
  /** Every identifier inside is handed out by the server. */
  specifications: AttributeSpecification[];
}
