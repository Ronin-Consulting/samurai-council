import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { ChatPage } from './chat-page';
import { ConversationStore } from '../services/conversation.store';
import { CouncilStreamService, StreamCallbacks } from '../services/council-stream.service';
import { AssistantMessage, Conversation } from '../models';

describe('ChatPage', () => {
  it('ignores a stale stream event for a conversation the user has since navigated away from', async () => {
    let capturedCb!: StreamCallbacks;
    const streamStub = {
      run: (_id: string, _content: string, cb: StreamCallbacks) => {
        capturedCb = cb;
        return Promise.resolve();
      },
    };

    TestBed.configureTestingModule({
      imports: [ChatPage],
      providers: [
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({})) } },
        { provide: Router, useValue: { navigate: () => Promise.resolve(true) } },
        { provide: CouncilStreamService, useValue: streamStub },
      ],
    });

    const fixture = TestBed.createComponent(ChatPage);
    const page = fixture.componentInstance;
    const store = TestBed.inject(ConversationStore);

    const convA: Conversation = { id: 'conv-A', title: 'A', created_at: '', messages: [] };
    store.current.set(convA);

    await page.sendText('Question for A');
    // capturedCb now closes over convId = 'conv-A'; the stream hasn't emitted anything yet.

    // User navigates to a different conversation while A's stream is still in flight.
    const convB: Conversation = {
      id: 'conv-B',
      title: 'B',
      created_at: '',
      messages: [{ role: 'user', content: 'Question for B' }],
    };
    store.current.set(convB);

    // A stray SSE frame from A's now-abandoned stream arrives late.
    capturedCb.onEvent('stage3', { model: 'x', response: 'Answer for A', tool_usages: [] });

    expect(store.current()?.id).toBe('conv-B');
    const lastMessage = store.current()!.messages[store.current()!.messages.length - 1] as AssistantMessage;
    expect(lastMessage.stage3).toBeUndefined();
  });
});
