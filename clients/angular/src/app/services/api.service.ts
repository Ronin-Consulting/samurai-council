import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Conversation, ConversationSummary } from '../models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);

  listConversations(): Observable<ConversationSummary[]> {
    return this.http.get<ConversationSummary[]>('/api/conversations');
  }

  getConversation(id: string): Observable<Conversation> {
    return this.http.get<Conversation>(`/api/conversations/${id}`);
  }

  createConversation(title?: string): Observable<Conversation> {
    return this.http.post<Conversation>('/api/conversations', { title: title ?? null });
  }

  updateTitle(id: string, title: string): Observable<void> {
    return this.http.put<void>(`/api/conversations/${id}/title`, { title });
  }

  deleteConversation(id: string): Observable<void> {
    return this.http.delete<void>(`/api/conversations/${id}`);
  }

  bulkDelete(ids: string[]): Observable<{ deleted: number }> {
    return this.http.post<{ deleted: number }>('/api/conversations/delete', { ids });
  }

  exportUrl(conversationId: string, format: 'pdf' | 'xlsx' | 'docx'): string {
    return `/api/conversations/${conversationId}/export?format=${format}`;
  }
}
