import type { ListEnvelope, Envelope, PagingQuery } from "@/api/models/common";
import type {
  Conversation,
  ConversationId,
  CreateConversation,
  Message,
  MessageId,
  UpdateConversation,
} from "@/api/models/messaging";
import type { UserLogin } from "@/api/models/community";
import type { Patch } from "@/api/models";
import Api from "@/api";
import { BbRenderMode } from "@/api/bbRenderMode";

export default new (class MessagingApi {
  // Conversations
  public getConversations(q: PagingQuery) {
    return Api.get<ListEnvelope<Conversation>>("conversations", q);
  }

  public async getOrCreateDirectConversation(login: UserLogin) {
    // POST creates conversation if it doesn't exist, returns existing if present
    return Api.post<Envelope<Conversation>>(`conversations/direct/${login}`);
  }

  public getConversation(id: ConversationId) {
    return Api.get<Envelope<Conversation>>(`conversations/${id}`);
  }

  public markConversationAsRead(id: ConversationId) {
    return Api.post(`conversations/${id}/messages/read`);
  }

  /**
   * Create a new group conversation
   */
  public createConversation(input: CreateConversation) {
    return Api.post<Envelope<Conversation>>("conversations", input);
  }

  /**
   * Update an existing conversation (title and/or participants)
   */
  public updateConversation(id: ConversationId, input: UpdateConversation) {
    return Api.patch<Envelope<Conversation>>(`conversations/${id}`, input);
  }

  // Messages
  public getMessages(conversationId: ConversationId, q: PagingQuery) {
    return Api.get<ListEnvelope<Message>>(
      `conversations/${conversationId}/messages`,
      q,
    );
  }

  public sendMessage(conversationId: ConversationId, text: string) {
    return Api.post<Envelope<Message>>(
      `conversations/${conversationId}/messages`,
      { text },
    );
  }

  public getMessage(id: MessageId) {
    return Api.get<Envelope<Message>>(`messages/${id}`);
  }

  /**
   * Get message with BBCode text for editing
   */
  public getMessageForEdit(id: MessageId) {
    return Api.get<Envelope<Message>>(
      `messages/${id}`,
      undefined,
      BbRenderMode.Bb,
    );
  }

  public updateMessage(id: MessageId, message: Patch<Message>) {
    return Api.patch<Envelope<Message>>(`messages/${id}`, message);
  }

  public deleteMessage(id: MessageId) {
    return Api.delete(`messages/${id}`);
  }

  // Message likes
  public likeMessage(id: MessageId) {
    return Api.post<Envelope<Message>>(`messages/${id}/likes`);
  }

  public unlikeMessage(id: MessageId) {
    return Api.delete(`messages/${id}/likes`);
  }
})();
