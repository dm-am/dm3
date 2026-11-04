import type { Envelope, ListEnvelope } from "@/shared/api/models/common";
import type {
  AttributeSchemaTemplate,
  AttributeSchemaTemplateRequest,
} from "../model/types";
import { Api } from "@/shared/api";
import type { RequestOptions } from "@/shared/api/interceptor";

/** Starting character sheets: read by the game creation form, written by senior moderation. */
export default new (class AttributeSchemaTemplateApi {
  /** Every template with its specifications, default first, so the form opens on one request. */
  public getTemplates(options?: RequestOptions) {
    return Api.get<ListEnvelope<AttributeSchemaTemplate>>(
      "attribute-schema-templates",
      undefined,
      undefined,
      options,
    );
  }

  public getTemplate(id: string, options?: RequestOptions) {
    return Api.get<Envelope<AttributeSchemaTemplate>>(
      `attribute-schema-templates/${id}`,
      undefined,
      undefined,
      options,
    );
  }

  public createTemplate(template: AttributeSchemaTemplateRequest) {
    return Api.post<Envelope<AttributeSchemaTemplate>>(
      "attribute-schema-templates",
      template,
    );
  }

  /** Replaces every writable field; the default flag is not one of them. */
  public updateTemplate(id: string, template: AttributeSchemaTemplateRequest) {
    return Api.put<Envelope<AttributeSchemaTemplate>>(
      `attribute-schema-templates/${id}`,
      template,
    );
  }

  public deleteTemplate(id: string, options?: RequestOptions) {
    return Api.delete(`attribute-schema-templates/${id}`, undefined, options);
  }

  /** Moves the default flag onto this template. */
  public makeDefault(id: string) {
    return Api.post(`attribute-schema-templates/${id}/default`);
  }
})();
